using System.Text.Json;
using Azure.ResourceManager.Kusto;
using AzureMcp.Arguments;
using AzureMcp.Commands.Kusto;
using AzureMcp.Models;
using AzureMcp.Services.Interfaces;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;

namespace AzureMcp.Services.Azure.Kusto;

public sealed class KustoService(
    ISubscriptionService subscriptionService,
    ITenantService tenantService,
    ICacheService cacheService) : BaseAzureService(tenantService), IKustoService
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

    private const string KUSTO_CLUSTERS_CACHE_KEY = "kusto_clusters";
    private const string KUSTO_ADMINPROVIDER_CACHE_KEY = "kusto_adminprovider";
    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromHours(1);
    private static readonly TimeSpan PROVIDER_CACHE_DURATION = TimeSpan.FromHours(2);

    // Provider cache key generator
    private static string GetProviderCacheKey(KustoConnectionStringBuilder kcsb)
        => $"{KUSTO_ADMINPROVIDER_CACHE_KEY}_{kcsb.DataSource}_{kcsb.InitialCatalog}_{kcsb.Authority}_{kcsb.ToString()}";

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

        await foreach (var cluster in subscription.GetKustoClustersAsync())
        {
            if (cluster?.Data?.Name != null)
            {
                clusters.Add(cluster.Data.Name);
            }
        }
        await _cacheService.SetAsync(cacheKey, clusters, CACHE_DURATION);

        return clusters;
    }

    public async Task<JsonElement> GetCluster(
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
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
                var json = JsonSerializer.SerializeToElement<KustoClusterResource>(cluster);
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code

                return json;
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

        var cslAdminProvider = await GetOrCreateCslAdminProvider(kcsb);

        var clientRequestProperties = CreateClientRequestProperties();
        var result = new List<string>();
        using (var reader = await cslAdminProvider.ExecuteControlCommandAsync(
            clusterName,
            ".show databases",
            clientRequestProperties))
        {
            while (reader.Read())
            {
                result.Add(reader["DatabaseName"].ToString()!);
            }
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

        var cslAdminProvider = await GetOrCreateCslAdminProvider(kcsb);

        var clientRequestProperties = CreateClientRequestProperties();
        var result = new List<string>();
        using (var reader = await cslAdminProvider.ExecuteControlCommandAsync(
            databaseName,
            ".show tables",
            clientRequestProperties))
        {
            while (reader.Read())
            {
                result.Add(reader["TableName"].ToString()!);
            }
        }

        return result;
    }

    public async Task<List<JsonElement>> GetTableSchema(
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

    public async Task<List<JsonElement>> GetTableSchema(
        string clusterUri,
        string databaseName,
        string tableName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(clusterUri, databaseName, tableName);

        var kcsb = await CreateKustoConnectionStringBuilder(clusterUri, authMethod, null, tenant);

        var cslAdminProvider = await GetOrCreateCslAdminProvider(kcsb);
        var clientRequestProperties = CreateClientRequestProperties();

        var result = new List<JsonElement>();
        using (var reader = await cslAdminProvider.ExecuteControlCommandAsync(
            databaseName,
            $".show table {tableName} cslschema",
            clientRequestProperties))
        {
            while (reader.Read())
            {
                var jsonString = reader["Schema"].ToString()!;
                using var doc = JsonDocument.Parse(jsonString);
                result.Add(doc.RootElement.Clone());
            }
        }

        return result;
    }

    public async Task<List<JsonElement>> QueryItems(
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

    public async Task<List<JsonElement>> QueryItems(
        string clusterUri,
        string databaseName,
        string query,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(clusterUri, databaseName, query);

        var kcsb = await CreateKustoConnectionStringBuilder(
            clusterUri,
            authMethod,
            null,
            tenant);

        var cslQueryProvider = await GetOrCreateCslQueryProvider(kcsb);
        var clientRequestProperties = CreateClientRequestProperties();

        var results = new List<JsonElement>();
        using (var reader = await cslQueryProvider.ExecuteQueryAsync(databaseName, query, clientRequestProperties))
        {
            while (reader.Read())
            {
                var dict = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    dict[reader.GetName(i)] = reader.GetValue(i);
                }
                var json = JsonSerializer.SerializeToElement(dict, KustoJsonContext.Default.QueryCommandResult);
                results.Add(json);
            }
        }

        return results;
    }

    private async Task<ICslAdminProvider> GetOrCreateCslAdminProvider(KustoConnectionStringBuilder kcsb)
    {
        var providerCacheKey = GetProviderCacheKey(kcsb);
        var cslAdminProvider = await _cacheService.GetAsync<ICslAdminProvider>(providerCacheKey, PROVIDER_CACHE_DURATION);
        if (cslAdminProvider == null)
        {
            cslAdminProvider = KustoClientFactory.CreateCslAdminProvider(kcsb);
            await _cacheService.SetAsync(providerCacheKey, cslAdminProvider, PROVIDER_CACHE_DURATION);
        }

        return cslAdminProvider;
    }

    private async Task<ICslQueryProvider> GetOrCreateCslQueryProvider(KustoConnectionStringBuilder kcsb)
    {
        var providerCacheKey = GetProviderCacheKey(kcsb) + "_query";
        var cslQueryProvider = await _cacheService.GetAsync<ICslQueryProvider>(providerCacheKey, PROVIDER_CACHE_DURATION);
        if (cslQueryProvider == null)
        {
            cslQueryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);
            await _cacheService.SetAsync(providerCacheKey, cslQueryProvider, PROVIDER_CACHE_DURATION);
        }

        return cslQueryProvider;
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
                {
                    throw new ArgumentNullException(nameof(connectionString));
                }

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

    private async Task<string> GetClusterUri(string subscriptionId, string clusterName, string? tenant, RetryPolicyArguments? retryPolicy)
    {
        var cluster = await GetCluster(subscriptionId, clusterName, tenant, retryPolicy);

        var clusterUri = cluster.GetProperty("Data").GetProperty("ClusterUri").GetString();

        if (string.IsNullOrEmpty(clusterUri))
        {
            throw new Exception($"Could not retrieve URI for cluster '{clusterName}'");
        }

        return clusterUri!;
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
