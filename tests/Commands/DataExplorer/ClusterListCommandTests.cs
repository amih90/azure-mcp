// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Arguments;
using AzureMcp.Commands.DataExplorer;
using AzureMcp.Models.Argument;
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace AzureMcp.Tests.Commands.DataExplorer;

public sealed class ClusterListCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDataExplorerService _dataExplorerService;
    private readonly ILogger<ClusterListCommand> _logger;

    public ClusterListCommandTests()
    {
        _dataExplorerService = Substitute.For<IDataExplorerService>();
        _logger = Substitute.For<ILogger<ClusterListCommand>>();

        var collection = new ServiceCollection();
        collection.AddSingleton(_dataExplorerService);

        _serviceProvider = collection.BuildServiceProvider();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsClusters_WhenClustersExist()
    {
        // Arrange
        var expectedClusters = new List<string> { "clusterA", "clusterB" };
        _dataExplorerService.ListClusters(
            "sub123", Arg.Any<string>(), Arg.Any<RetryPolicyArguments>())
            .Returns(expectedClusters);
        
        var command = new ClusterListCommand(_logger);
        var args = command.GetCommand().Parse(["--subscription", "sub123"]);
        var context = new CommandContext(_serviceProvider);


        // Act
        var response = await command.ExecuteAsync(context, args);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Results);

        var json = JsonSerializer.Serialize(response.Results);
        var result = JsonSerializer.Deserialize<ClusterListResult>(json);

        Assert.NotNull(result);
        Assert.Equal(expectedClusters, result.Clusters);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenNoClustersExist()
    {
        // Arrange
        _dataExplorerService.ListClusters("sub123", null, null)
            .Returns([]);

        var command = new ClusterListCommand(_logger);
        var args = command.GetCommand().Parse(["--subscription", "sub123"]);
        var context = new CommandContext(_serviceProvider);

        // Act
        var response = await command.ExecuteAsync(context, args);

        // Assert
        Assert.NotNull(response);
        Assert.Null(response.Results);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesException_AndSetsException()
    {
        // Arrange
        var expectedError = "Test error. To mitigate this issue, please refer to the troubleshooting guidelines here at https://aka.ms/azmcp/troubleshooting.";
        var subscriptionId = "sub123";
        var defaultRetryPolicy = new RetryPolicyArguments
        {
            DelaySeconds = ArgumentDefinitions.RetryPolicy.Delay.DefaultValue,
            MaxDelaySeconds = ArgumentDefinitions.RetryPolicy.MaxDelay.DefaultValue,
            MaxRetries = ArgumentDefinitions.RetryPolicy.MaxRetries.DefaultValue,
            Mode = ArgumentDefinitions.RetryPolicy.Mode.DefaultValue,
            NetworkTimeoutSeconds = ArgumentDefinitions.RetryPolicy.NetworkTimeout.DefaultValue
        };
        
        // Arrange
        _dataExplorerService.ListClusters(subscriptionId, null, defaultRetryPolicy)
            .ThrowsAsync(new Exception("Test error"));

        var command = new ClusterListCommand(_logger);
        var args = command.GetCommand().Parse(["--subscription", subscriptionId]);
        var context = new CommandContext(_serviceProvider);

        // Act
        var response = await command.ExecuteAsync(context, args);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(500, response.Status);
        Assert.Equal(expectedError, response.Message);
    }

    private sealed class ClusterListResult
    {
        [JsonPropertyName("clusters")]
        public List<string>? Clusters { get; set; }
    }
}
