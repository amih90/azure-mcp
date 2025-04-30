using AzureMcp.Arguments;
using AzureMcp.Models;
using System.Text.Json;

namespace AzureMcp.Services.Interfaces;

public interface IDataExplorerService
{
    Task<List<string>> ListClusters(
        string subscriptionId,
        string? tenant = null,
        RetryPolicyArguments? retryPolicy = null);

    Task<JsonDocument> GetCluster(
        string subscriptionId,
        string clusterName,
        string? tenant = null,
        RetryPolicyArguments? retryPolicy = null);

    Task<List<string>> ListDatabases(
        string clusterUri,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null);

    Task<List<string>> ListDatabases(
        string subscriptionId,
        string clusterName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null);

    Task<List<JsonDocument>> QueryItems(
        string clusterUri,
        string databaseName,
        string query,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null);

    Task<List<JsonDocument>> QueryItems(
        string subscriptionId,
        string clusterName,
        string databaseName,
        string query,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null);
}
