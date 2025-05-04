using System.Text.Json.Nodes;
using Azure.ResourceManager.Kusto;
using AzureMcp.Arguments;
using AzureMcp.Commands.Kusto;
using AzureMcp.Models;
using AzureMcp.Services.Interfaces;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace AzureMcp.Services.Azure.Kusto;

public sealed class KustoService(
    ISubscriptionService subscriptionService,
    ICacheService cacheService) : BaseAzureService, IKustoService
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private const string KUSTO_CLUSTERS_CACHE_KEY = "kusto_clusters";
    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromHours(1);

    private ClientRequestProperties CreateClientRequestProperties()
    {
        return new ClientRequestProperties
        {
            ClientRequestId = $"AzMcp;{Guid.NewGuid()}",
            Application = "AzureMCP"
        };
    }

    public async Task<List<string>> ListClusters(
        string subscriptionId,
        string? tenant = null,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId);

        // Create cache key
        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"{KUSTO_CLUSTERS_CACHE_KEY}_{subscriptionId}"
            : $"{KUSTO_CLUSTERS_CACHE_KEY}_{subscriptionId}_{tenant}";

        // Try to get from cache first
        var cachedClusters = await _cacheService.GetAsync<List<string>>(cacheKey, CACHE_DURATION);
        if (cachedClusters != null)
        {
            return cachedClusters;
        }

        var subscription = await _subscriptionService.GetSubscription(subscriptionId, tenant, retryPolicy);
        var clusters = new List<string>();
        try
        {
            await foreach (var cluster in subscription.GetKustoClustersAsync())
            {
                if (cluster?.Data?.Name != null)
                {
                    clusters.Add(cluster.Data.Name);
                }
            }
            await _cacheService.SetAsync(cacheKey, clusters, CACHE_DURATION);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Kusto clusters: {ex.Message}", ex);
        }
        return clusters;
    }

    private static async Task<string> GetClusterUri(string subscriptionId, string clusterName, string? tenant, RetryPolicyArguments? retryPolicy)
    {
        var kustoService = new KustoService(null!, null!); // This should be replaced with DI or refactored for static context
        var cluster = await kustoService.GetCluster(subscriptionId, clusterName, tenant, retryPolicy);
        if (cluster is null)
            throw new Exception($"Kusto cluster '{clusterName}' not found in subscription '{subscriptionId}'.");
        var clusterUri = cluster?["clusterUri"]?.ToString();
        if (string.IsNullOrEmpty(clusterUri))
            throw new Exception($"Could not retrieve URI for cluster '{clusterName}'");
        return clusterUri!;
    }

    public async Task<JsonNode> GetCluster(
        string subscriptionId,
        string clusterName,
        string? tenant = null,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId, clusterName);

        var subscription = await _subscriptionService.GetSubscription(subscriptionId, tenant, retryPolicy);
        await foreach (var cluster in subscription.GetKustoClustersAsync())
        {
            if (string.Equals(cluster.Data.Name, clusterName, StringComparison.OrdinalIgnoreCase))
            {
                // Serialize the cluster data to JSON using source generation context
                var json = System.Text.Json.JsonSerializer.SerializeToNode(cluster.Data, KustoJsonContext.Default.ClusterGetCommandResult);
                return json!;
            }
        }
        throw new Exception($"Kusto cluster '{clusterName}' not found in subscription '{subscriptionId}'.");
    }

    public async Task<List<string>> ListDatabases(
        string subscriptionId,
        string clusterName,
        string? tenant = null,
        AuthMethod? authMethod =
        AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId, clusterName);

        string clusterUri = await GetClusterUri(subscriptionId, clusterName, tenant, retryPolicy);
        return await ListDatabases(clusterUri, tenant, authMethod, retryPolicy);
    }

    public async Task<List<string>> ListDatabases(
        string clusterUri,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(clusterUri);

        var clusterName = GetClusterNameFromUri(clusterUri);
        ValidateRequiredParameters(clusterName);

        var kcsb = await CreateKustoConnectionStringBuilder(
            clusterUri,
            authMethod,
            null,
            tenant);

        using var cslAdminProvider = KustoClientFactory.CreateCslAdminProvider(kcsb);
        var clientRequestProperties = CreateClientRequestProperties();
        var reader = await cslAdminProvider.ExecuteControlCommandAsync(
            clusterName,
            ".show databases",
            clientRequestProperties);
        var result = new List<string>();
        while (reader.Read())
        {
            result.Add(reader["DatabaseName"].ToString()!);
        }
        return result;
    }

    public async Task<List<string>> ListTables(
        string subscriptionId,
        string clusterName,
        string databaseName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId, clusterName, databaseName);

        var clusterUri = await GetClusterUri(subscriptionId, clusterName, tenant, retryPolicy);
        return await ListTables(clusterUri, databaseName, tenant, authMethod, retryPolicy);
    }

    public async Task<List<string>> ListTables(
        string clusterUri,
        string databaseName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(clusterUri, databaseName);

        var clusterName = GetClusterNameFromUri(clusterUri);
        ValidateRequiredParameters(clusterName);

        var kcsb = await CreateKustoConnectionStringBuilder(
            clusterUri,
            authMethod,
            null,
            tenant);

        using var cslAdminProvider = KustoClientFactory.CreateCslAdminProvider(kcsb);
        var clientRequestProperties = CreateClientRequestProperties();
        var reader = await cslAdminProvider.ExecuteControlCommandAsync(
            databaseName,
            ".show tables",
            clientRequestProperties);
        var result = new List<string>();
        while (reader.Read())
        {
            result.Add(reader["TableName"].ToString()!);
        }
        return result;
    }

    public async Task<List<JsonNode>> GetTableSchema(
        string subscriptionId,
        string clusterName,
        string databaseName,
        string tableName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null)
    {
        var clusterUri = await GetClusterUri(subscriptionId, clusterName, tenant, retryPolicy);
        return await GetTableSchema(clusterUri, databaseName, tableName, tenant, authMethod, retryPolicy);
    }

    public async Task<List<JsonNode>> GetTableSchema(
        string clusterUri,
        string databaseName,
        string tableName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(clusterUri, databaseName, tableName);
        var kcsb = await CreateKustoConnectionStringBuilder(clusterUri, authMethod, null, tenant);
        using var cslAdminProvider = KustoClientFactory.CreateCslAdminProvider(kcsb);
        var clientRequestProperties = CreateClientRequestProperties();
        var reader = await cslAdminProvider.ExecuteControlCommandAsync(
            databaseName,
            $".show table {tableName} cslschema",
            clientRequestProperties);
        var result = new List<JsonNode>();
        while (reader.Read())
        {
            var jsonString = reader["Schema"].ToString()!;
            var json = JsonNode.Parse(jsonString);
            if (json != null)
                result.Add(json);
        }
        return result;
    }

    public async Task<List<JsonNode>> QueryItems(
        string subscriptionId,
        string clusterName,
        string databaseName,
        string query,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId, clusterName, databaseName, query);

        string clusterUri = await GetClusterUri(subscriptionId, clusterName, tenant, retryPolicy);

        return await QueryItems(clusterUri, databaseName, query, tenant, authMethod, retryPolicy);
    }

    public async Task<List<JsonNode>> QueryItems(
        string clusterUri,
        string databaseName,
        string query, string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(clusterUri, databaseName, query);

        var kcsb = await CreateKustoConnectionStringBuilder(
            clusterUri,
            authMethod,
            null,
            tenant);

        using var cslAdminProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);
        var clientRequestProperties = CreateClientRequestProperties();
        var reader = await cslAdminProvider.ExecuteQueryAsync(databaseName, query, clientRequestProperties);
        var results = new List<JsonNode>();

        try
        {
            while (reader.Read())
            {
                var dict = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    dict[reader.GetName(i)] = reader.GetValue(i);
                }
                var json = System.Text.Json.JsonSerializer.SerializeToNode(dict, KustoJsonContext.Default.QueryCommandResult);
                if (json != null)
                    results.Add(json);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error executing query: {ex.Message}", ex);
        }

        return results;
    }

    private async Task<KustoConnectionStringBuilder> CreateKustoConnectionStringBuilder(
        string uri,
        AuthMethod? authMethod,
        string? connectionString = null,
        string? tenant = null)
    {
        switch (authMethod)
        {
            case AuthMethod.Key:
                throw new NotSupportedException("Not Supported. Supported Types are: AAD credential or connection string.");
            case AuthMethod.ConnectionString:
                if (string.IsNullOrEmpty(connectionString))
                    throw new ArgumentNullException(nameof(connectionString));
                return new KustoConnectionStringBuilder(connectionString);
            case AuthMethod.Credential:
            default:
                var credential = await GetCredential(tenant);
                var builder = new KustoConnectionStringBuilder(uri).WithAadAzureTokenCredentialsAuthentication(credential);
                if (!string.IsNullOrEmpty(tenant))
                {
                    builder.Authority = $"https://login.microsoftonline.com/{tenant}";
                }
                return builder;
        }
    }

    public static string GetClusterNameFromUri(string clusterUri)
    {
        ValidateRequiredParameters(clusterUri);

        var uri = new Uri(clusterUri);
        var host = uri.Host;
        var clusterName = host.Split('.')[0];
        return clusterName;
    }
}
