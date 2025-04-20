// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.Argument;
using System.Text.Json.Serialization;

namespace AzureMcp.Arguments.DataExplorer;

public class BaseDatabaseArguments : SubscriptionArguments
{
    [JsonPropertyName(ArgumentDefinitions.DataExplorer.Uri)]
    public string? ClusterUri { get; set; }
}