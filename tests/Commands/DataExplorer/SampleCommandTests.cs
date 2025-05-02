// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Arguments;
using AzureMcp.Arguments.DataExplorer;
using AzureMcp.Commands.DataExplorer;
using AzureMcp.Models;
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.CommandLine.Parsing;
using System.Text.Json;
using Xunit;

namespace AzureMcp.Tests.Commands.DataExplorer;

public sealed class SampleCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDataExplorerService _dataExplorerService;
    private readonly ILogger<SampleCommand> _logger;

    public SampleCommandTests()
    {
        _dataExplorerService = Substitute.For<IDataExplorerService>();
        _logger = Substitute.For<ILogger<SampleCommand>>();
        var collection = new ServiceCollection();
        collection.AddSingleton(_dataExplorerService);
        _serviceProvider = collection.BuildServiceProvider();
    }

    public static IEnumerable<object[]> SampleArgumentMatrix()
    {
        yield return new object[] { "--subscription sub1 --cluster-name mycluster --database-name db1 --table-name table1", false };
        yield return new object[] { "--cluster-uri https://mycluster.kusto.windows.net --database-name db1 --table-name table1", true };
    }

    [Theory]
    [MemberData(nameof(SampleArgumentMatrix))]
    public async Task ExecuteAsync_ReturnsSampleResults(string cliArgs, bool useClusterUri)
    {
        // Arrange
        var expectedJson = JsonDocument.Parse("[{\"foo\":42}]");
        if (useClusterUri)
        {
            _dataExplorerService.QueryItems(
                "https://mycluster.kusto.windows.net",
                "db1",
                "table1 | sample 10",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(new List<JsonDocument> { expectedJson });
        }
        else
        {
            _dataExplorerService.QueryItems(
                "sub1", "mycluster", "db1", "table1 | sample 10",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(new List<JsonDocument> { expectedJson });
        }
        var command = new SampleCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse(cliArgs);
        var context = new CommandContext(_serviceProvider);

        // Act
        var response = await command.ExecuteAsync(context, args);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Results);
        var json = JsonSerializer.Serialize(response.Results);
        var results = JsonSerializer.Deserialize<List<JsonDocument>>(json);
        Assert.NotNull(results);
        Assert.Equal(1, results?.Count);
        var actualJson = results?[0].RootElement.GetRawText();
        var expectedJsonText = expectedJson.RootElement.GetRawText();
        Assert.Equal(expectedJsonText, actualJson);
    }

    [Theory]
    [MemberData(nameof(SampleArgumentMatrix))]
    public async Task ExecuteAsync_ReturnsNull_WhenNoResults(string cliArgs, bool useClusterUri)
    {
        if (useClusterUri)
        {
            _dataExplorerService.QueryItems(
                "https://mycluster.kusto.windows.net",
                "db1",
                "table1 | sample 10",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(new List<JsonDocument>());
        }
        else
        {
            _dataExplorerService.QueryItems(
                "sub1", "mycluster", "db1", "table1 | sample 10",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(new List<JsonDocument>());
        }
        var command = new SampleCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse(cliArgs);
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);
        Assert.NotNull(response);
        Assert.Null(response.Results);
    }

    [Theory]
    [MemberData(nameof(SampleArgumentMatrix))]
    public async Task ExecuteAsync_HandlesException_AndSetsException(string cliArgs, bool useClusterUri)
    {
        var expectedError = "Test error. To mitigate this issue, please refer to the troubleshooting guidelines here at https://aka.ms/azmcp/troubleshooting.";
        if (useClusterUri)
        {
            _dataExplorerService.QueryItems(
                "https://mycluster.kusto.windows.net",
                "db1",
                "table1 | sample 10",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(Task.FromException<List<JsonDocument>>(new Exception("Test error")));
        }
        else
        {
            _dataExplorerService.QueryItems(
                "sub1", "mycluster", "db1", "table1 | sample 10",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(Task.FromException<List<JsonDocument>>(new Exception("Test error")));
        }
        var command = new SampleCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse(cliArgs);
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);
        Assert.NotNull(response);
        Assert.Equal(500, response.Status);
        Assert.Equal(expectedError, response.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenMissingRequiredArguments()
    {
        var command = new SampleCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse("");
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);
        Assert.NotNull(response);
        Assert.Equal(400, response.Status);
    }
}