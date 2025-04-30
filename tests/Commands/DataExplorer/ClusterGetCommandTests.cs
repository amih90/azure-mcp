// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Arguments;
using AzureMcp.Commands.DataExplorer;
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace AzureMcp.Tests.Commands.DataExplorer;

public sealed class ClusterGetCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDataExplorerService _dataExplorerService;
    private readonly ILogger<ClusterGetCommand> _logger;

    public ClusterGetCommandTests()
    {
        _dataExplorerService = Substitute.For<IDataExplorerService>();
        _logger = Substitute.For<ILogger<ClusterGetCommand>>();
        var collection = new ServiceCollection();
        collection.AddSingleton(_dataExplorerService);
        _serviceProvider = collection.BuildServiceProvider();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCluster_WhenClusterExists()
    {
        var expectedCluster = JsonDocument.Parse("{\"name\":\"clusterA\"}");
        _dataExplorerService.GetCluster(
            "sub123", "clusterA", Arg.Any<string>(), Arg.Any<RetryPolicyArguments>())
            .Returns((Task<JsonDocument>)(object)Task.FromResult<JsonDocument?>(expectedCluster));
        var command = new ClusterGetCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse("--subscription sub123 --cluster-name clusterA");
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);

        Assert.NotNull(response);
        Assert.NotNull(response.Results);
        var json = JsonSerializer.Serialize(response.Results);
        var result = JsonSerializer.Deserialize<ClusterGetResult>(json);
        Assert.NotNull(result);
        Assert.NotNull(result.Cluster);
        Assert.Equal("clusterA", result.Cluster.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenClusterDoesNotExist()
    {
        _dataExplorerService.GetCluster(
            "sub123", "clusterA", Arg.Any<string>(), Arg.Any<RetryPolicyArguments>())
            .Returns((Task<JsonDocument>)(object)Task.FromResult<JsonDocument?>(null));
        var command = new ClusterGetCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse("--subscription sub123 --cluster-name clusterA");
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);

        Assert.NotNull(response);
        Assert.Null(response.Results);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesException_AndSetsException()
    {
        var expectedError = "Test error. To mitigate this issue, please refer to the troubleshooting guidelines here at https://aka.ms/azmcp/troubleshooting.";
        _dataExplorerService.GetCluster(
            "sub123", "clusterA", Arg.Any<string>(), Arg.Any<RetryPolicyArguments>())
            .ThrowsAsync(new Exception("Test error"));
        var command = new ClusterGetCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse("--subscription sub123 --cluster-name clusterA");
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);

        Assert.NotNull(response);
        Assert.Equal(500, response.Status);
        Assert.Equal(expectedError, response.Message);
    }

    private sealed class ClusterGetResult
    {
        [JsonPropertyName("cluster")]
        public JsonDocument? Cluster { get; set; }
    }
}
