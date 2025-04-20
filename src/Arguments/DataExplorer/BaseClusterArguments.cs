// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.Argument;
using System.Text.Json.Serialization;

namespace AzureMcp.Arguments.DataExplorer;

public class BaseClusterArguments : SubscriptionArguments
{
    [JsonPropertyName(ArgumentDefinitions.DataExplorer.ClusterName)]
    public string? Cluster { get; set; }
}