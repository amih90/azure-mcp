// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.Argument;
using System.Text.Json.Serialization;

namespace AzureMcp.Arguments.Kusto;

public class BaseDatabaseArguments : BaseClusterArguments
{
    [JsonPropertyName(ArgumentDefinitions.Kusto.DatabaseName)]
    public string? Database { get; set; }
}