// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.Argument;
using System.Text.Json.Serialization;

namespace AzureMcp.Arguments.Kusto;

public class QueryArguments : BaseDatabaseArguments
{
    [JsonPropertyName(ArgumentDefinitions.Kusto.QueryText)]
    public string? Query { get; set; }
}