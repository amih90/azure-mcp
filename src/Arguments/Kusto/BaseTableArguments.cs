// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.Argument;
using System.Text.Json.Serialization;

namespace AzureMcp.Arguments.Kusto;

public class BaseTableArguments : BaseDatabaseArguments
{
    [JsonPropertyName(ArgumentDefinitions.Kusto.TableName)]
    public string? Table { get; set; }
}