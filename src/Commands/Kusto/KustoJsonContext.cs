using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.ResourceManager.Kusto;

namespace AzureMcp.Commands.Kusto;

[JsonSerializable(typeof(ClusterListCommand.ClusterListCommandResult))]
[JsonSerializable(typeof(ClusterGetCommand.ClusterGetCommandResult))]
[JsonSerializable(typeof(DatabaseListCommand.DatabaseListCommandResult))]
[JsonSerializable(typeof(TableListCommand.TableListCommandResult))]
[JsonSerializable(typeof(TableSchemaCommand.TableSchemaCommandResult))]
[JsonSerializable(typeof(QueryCommand.QueryCommandResult))]
[JsonSerializable(typeof(SampleCommand.SampleCommandResult))]
[JsonSerializable(typeof(JsonElement))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class KustoJsonContext : JsonSerializerContext
{
}
