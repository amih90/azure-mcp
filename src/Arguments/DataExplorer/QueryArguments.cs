// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.Argument;
using System.Text.Json.Serialization;

namespace AzureMcp.Arguments.DataExplorer;

public class QueryArguments : BaseDatabaseArguments
{
    [JsonPropertyName(ArgumentDefinitions.DataExplorer.DatabaseName)]
    public string? Database { get; set; }

    [JsonPropertyName(ArgumentDefinitions.DataExplorer.QueryText)]
    public string? Query { get; set; }
}
