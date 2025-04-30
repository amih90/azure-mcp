// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.Argument;
using System.Text.Json.Serialization;

namespace AzureMcp.Arguments.DataExplorer;

public class BaseDatabaseArguments : BaseClusterArguments
{
    [JsonPropertyName(ArgumentDefinitions.DataExplorer.DatabaseName)]
    public string? Database { get; set; }
}