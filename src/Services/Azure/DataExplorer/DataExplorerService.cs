using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Kusto;
using Azure.ResourceManager.Kusto.Models;
using AzureMcp.Arguments;
using AzureMcp.Models;
using AzureMcp.Models.Argument;
using AzureMcp.Services.Interfaces;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;

namespace AzureMcp.Services.Azure.DataExplorer;

public sealed class DataExplorerService(ISubscriptionService subscriptionService, ICacheService cacheService) : BaseAzureService, IDataExplorerService
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private const string DATA_EXPLORER_CLUSTERS_CACHE_KEY = "adx_clusters";
    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromHours(1);

    public async Task<List<string>> ListClusters(
        string subscriptionId, 
        string? tenant = null, 
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId);

        // Create cache key
        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"{DATA_EXPLORER_CLUSTERS_CACHE_KEY}_{subscriptionId}"
            : $"{DATA_EXPLORER_CLUSTERS_CACHE_KEY}_{subscriptionId}_{tenant}";
        
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
            throw new Exception($"Error retrieving Data Explorer clusters: {ex.Message}", ex);
        }
        return clusters;
    }

    public async Task<JsonDocument> GetCluster(
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
                // Serialize the cluster data to JSON
                return JsonSerializer.SerializeToDocument(cluster.Data);
            }
        }
        throw new Exception($"Data Explorer cluster '{clusterName}' not found in subscription '{subscriptionId}'.");
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

        using var queryProvider = KustoClientFactory.CreateCslAdminProvider(kcsb);
        var reader = await queryProvider.ExecuteControlCommandAsync(clusterName, ".show databases", null);
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

        using var queryProvider = KustoClientFactory.CreateCslAdminProvider(kcsb);
        var reader = await queryProvider.ExecuteControlCommandAsync(databaseName, ".show tables", null);
        var result = new List<string>();
        while (reader.Read())
        {
            result.Add(reader["TableName"].ToString()!);
        }
        return result;
    }

    public async Task<List<JsonDocument>> GetTableSchema(
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

    public async Task<List<JsonDocument>> GetTableSchema(
        string clusterUri,
        string databaseName,
        string tableName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(clusterUri, databaseName, tableName);
        var kcsb = await CreateKustoConnectionStringBuilder(clusterUri, authMethod, null, tenant);
        using var queryProvider = KustoClientFactory.CreateCslAdminProvider(kcsb);
        var command = $".show table {tableName} schema as json";
        var reader = await queryProvider.ExecuteControlCommandAsync(databaseName, command, null);
        var result = new List<JsonDocument>();
        while (reader.Read())
        {
            var jsonString = reader["Schema"].ToString()!;
            var json = JsonDocument.Parse(jsonString);
            result.Add(json);
            
        }
        return result;
    }

    public async Task<List<JsonDocument>> QueryItems(
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

    private async Task<string> GetClusterUri(string subscriptionId, string clusterName, string? tenant, RetryPolicyArguments? retryPolicy)
    {
        var cluster = await GetCluster(subscriptionId, clusterName, tenant, retryPolicy) ?? throw new Exception($"Data Explorer cluster '{clusterName}' not found in subscription '{subscriptionId}'.");

        if (!cluster.RootElement.TryGetProperty("ClusterUri", out var clusterUriElement))
        {
            throw new Exception($"Could not retrieve URI for cluster '{clusterName}'");
        }

        var clusterUri = clusterUriElement.GetString();
        if (string.IsNullOrEmpty(clusterUri))
        {
            throw new Exception($"Could not retrieve URI for cluster '{clusterName}'");
        }

        return clusterUri!;
    }

    public async Task<List<JsonDocument>> QueryItems(
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
            
        using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);
        var reader = await queryProvider.ExecuteQueryAsync(databaseName, query, null);
        var results = new List<JsonDocument>();

        try
        {
            while (reader.Read())
            {
                var dict = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    dict[reader.GetName(i)] = reader.GetValue(i);
                }
                var json = JsonSerializer.SerializeToDocument(dict);
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
                throw new NotSupportedException("Data Explorer data plane does not support key-based authentication via ARM. Use AAD credential or connection string.");
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
