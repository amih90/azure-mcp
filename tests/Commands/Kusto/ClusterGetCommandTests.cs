// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AzureMcp.Arguments;
using AzureMcp.Commands.Kusto;
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AzureMcp.Tests.Commands.Kusto;

public sealed class ClusterGetCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IKustoService _kusto;
    private readonly ILogger<ClusterGetCommand> _logger;

    public ClusterGetCommandTests()
    {
        _kusto = Substitute.For<IKustoService>();
        _logger = Substitute.For<ILogger<ClusterGetCommand>>();
        var collection = new ServiceCollection();
        collection.AddSingleton(_kusto);
        _serviceProvider = collection.BuildServiceProvider();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCluster_WhenClusterExists()
    {
        var expectedCluster = JsonDocument.Parse("{\"name\":\"clusterA\"}").RootElement.Clone();
        _kusto.GetCluster(
            "sub123", "clusterA", Arg.Any<string>(), Arg.Any<RetryPolicyArguments>())
            .Returns(Task.FromResult(expectedCluster));
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
        Assert.Equal("clusterA", result.Cluster.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenClusterDoesNotExist()
    {
        _kusto.GetCluster(
            "sub123", "clusterA", Arg.Any<string>(), Arg.Any<RetryPolicyArguments>())
            .Returns(Task.FromResult(default(JsonElement)));
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
        _kusto.GetCluster(
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
        public JsonElement Cluster { get; set; }
    }
}
