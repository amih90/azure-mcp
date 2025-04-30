// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.Argument;
using System.Text.Json.Serialization;

namespace AzureMcp.Arguments.DataExplorer;

public class BaseClusterArguments : SubscriptionArguments
{
    [JsonPropertyName(ArgumentDefinitions.DataExplorer.ClusterName)]
    public string? ClusterName { get; set; }

    [JsonPropertyName(ArgumentDefinitions.DataExplorer.ClusterUriName)]
    public string? ClusterUri { get; set; }
}