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

    public async Task<List<string>> ListClusters(string subscriptionId, string? tenant = null, RetryPolicyArguments? retryPolicy = null)
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

    public async Task<JsonDocument> GetCluster(string subscriptionId, string clusterName, string? tenant = null, RetryPolicyArguments? retryPolicy = null)
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

    public async Task<List<string>> ListDatabases(string subscriptionId, string clusterUri, string? tenant = null, AuthMethod? authMethod = AuthMethod.Credential, RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId, clusterUri);

        var kcsb = CreateKustoConnectionStringBuilder(
            clusterUri,
            authMethod,
            null,
            tenant);

        using var queryProvider = KustoClientFactory.CreateCslAdminProvider(kcsb);
        var reader = await queryProvider.ExecuteControlCommandAsync("romealertsdeveus", ".show databases", null);
        var result = new List<string>();
        while (reader.Read())
        {
            result.Add(reader["DatabaseName"].ToString()!);
        }
        return result;
    }

    public async Task<List<JsonDocument>> QueryItems(string subscriptionId, string clusterUri, string databaseName, string query, string? tenant = null, AuthMethod? authMethod = AuthMethod.Credential, RetryPolicyArguments? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId, clusterUri, databaseName, query);

        var kcsb = CreateKustoConnectionStringBuilder(
            clusterUri,
            authMethod,
            null,
            tenant);
            
        using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);
        var reader = await queryProvider.ExecuteQueryAsync(databaseName, query, null);
        var results = new List<JsonDocument>();
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
        return results;
    }

    private KustoConnectionStringBuilder CreateKustoConnectionStringBuilder(
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
                var builder = new KustoConnectionStringBuilder(uri).WithAadAzureTokenCredentialsAuthentication(GetCredential(tenant));
                if (!string.IsNullOrEmpty(tenant))
                {
                    builder.Authority = $"https://login.microsoftonline.com/{tenant}";
                }
                return builder;
        }
    }
}
