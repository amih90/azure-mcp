// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models;
using AzureMcp.Models.Argument;
using System.Text.Json.Serialization;

namespace AzureMcp.Arguments.DataExplorer;

public class BaseClusterGlobalArguments : GlobalArguments
{
    [JsonPropertyName(ArgumentDefinitions.DataExplorer.UriName)]
    public string? ClusterUri { get; set; }
}