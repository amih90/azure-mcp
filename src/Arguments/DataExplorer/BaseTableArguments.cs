// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.Argument;
using System.Text.Json.Serialization;

namespace AzureMcp.Arguments.DataExplorer;

public class BaseTableArguments : BaseDatabaseArguments
{
    [JsonPropertyName(ArgumentDefinitions.DataExplorer.TableName)]
    public string? Table { get; set; }
}