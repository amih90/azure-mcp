using System.Text.Json.Nodes;
using AzureMcp.Arguments;
using AzureMcp.Models;

namespace AzureMcp.Services.Interfaces;

public interface IKustoService
{
    Task<List<string>> ListClusters(
        string subscriptionId,
        string? tenant = null,
        RetryPolicyArguments? retryPolicy = null);

    Task<JsonNode> GetCluster(
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

    Task<List<JsonNode>> QueryItems(
        string clusterUri,
        string databaseName,
        string query,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null);

    Task<List<JsonNode>> QueryItems(
        string subscriptionId,
        string clusterName,
        string databaseName,
        string query,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null);

    Task<List<string>> ListTables(
        string clusterUri,
        string databaseName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null);

    Task<List<string>> ListTables(
        string subscriptionId,
        string clusterName,
        string databaseName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null);

    Task<List<JsonNode>> GetTableSchema(
        string clusterUri,
        string databaseName,
        string tableName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null);

    Task<List<JsonNode>> GetTableSchema(
        string subscriptionId,
        string clusterName,
        string databaseName,
        string tableName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyArguments? retryPolicy = null);
}
