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
using System.Text.Json.Serialization;
using Xunit;

namespace AzureMcp.Tests.Commands.DataExplorer;

public sealed class DatabaseListCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDataExplorerService _dataExplorerService;
    private readonly ILogger<DatabaseListCommand> _logger;

    public DatabaseListCommandTests()
    {
        _dataExplorerService = Substitute.For<IDataExplorerService>();
        _logger = Substitute.For<ILogger<DatabaseListCommand>>();
        var collection = new ServiceCollection();
        collection.AddSingleton(_dataExplorerService);
        _serviceProvider = collection.BuildServiceProvider();
    }

    public static IEnumerable<object[]> DatabaseArgumentMatrix()
    {
        yield return new object[] { "--subscription sub1 --cluster-name mycluster", false };
        yield return new object[] { "--cluster-uri https://mycluster.kusto.windows.net", true };
    }

    [Theory]
    [MemberData(nameof(DatabaseArgumentMatrix))]
    public async Task ExecuteAsync_ReturnsDatabases(string cliArgs, bool useClusterUri)
    {
        // Arrange
        var expectedDatabases = new List<string> { "db1", "db2" };
        if (useClusterUri)
        {
            _dataExplorerService.ListDatabases(
                "https://mycluster.kusto.windows.net",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(expectedDatabases);
        }
        else
        {
            _dataExplorerService.ListDatabases(
                "sub1", "mycluster", Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(expectedDatabases);
        }
        var command = new DatabaseListCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse(cliArgs);
        var context = new CommandContext(_serviceProvider);

        // Act
        var response = await command.ExecuteAsync(context, args);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Results);
        var json = JsonSerializer.Serialize(response.Results);
        var result = JsonSerializer.Deserialize<DatabaseListResult>(json);
        Assert.NotNull(result);
        Assert.Equal(expectedDatabases, result.Databases);
    }

    [Theory]
    [MemberData(nameof(DatabaseArgumentMatrix))]
    public async Task ExecuteAsync_ReturnsNull_WhenNoDatabasesExist(string cliArgs, bool useClusterUri)
    {
        // Arrange
        if (useClusterUri)
        {
            _dataExplorerService.ListDatabases(
                "https://mycluster.kusto.windows.net",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns([]);
        }
        else
        {
            _dataExplorerService.ListDatabases(
                "sub1", "mycluster", Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns([]);
        }
        var command = new DatabaseListCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse(cliArgs);
        var context = new CommandContext(_serviceProvider);

        // Act
        var response = await command.ExecuteAsync(context, args);

        // Assert
        Assert.NotNull(response);
        Assert.Null(response.Results);
    }

    [Theory]
    [MemberData(nameof(DatabaseArgumentMatrix))]
    public async Task ExecuteAsync_HandlesException_AndSetsException(string cliArgs, bool useClusterUri)
    {
        // Arrange
        var expectedError = "Test error. To mitigate this issue, please refer to the troubleshooting guidelines here at https://aka.ms/azmcp/troubleshooting.";
        if (useClusterUri)
        {
            _dataExplorerService.ListDatabases(
                "https://mycluster.kusto.windows.net",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(Task.FromException<List<string>>(new Exception("Test error")));
        }
        else
        {
            _dataExplorerService.ListDatabases(
                "sub1", "mycluster", Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(Task.FromException<List<string>>(new Exception("Test error")));
        }
        var command = new DatabaseListCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse(cliArgs);
        var context = new CommandContext(_serviceProvider);

        // Act
        var response = await command.ExecuteAsync(context, args);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(500, response.Status);
        Assert.Equal(expectedError, response.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenMissingRequiredArguments()
    {
        var command = new DatabaseListCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse(""); // No arguments
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);

        Assert.NotNull(response);
        Assert.Equal(400, response.Status);
        Assert.Contains("Missing required", response.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenMissingAllRequiredArguments()
    {
        var command = new DatabaseListCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse(""); // No arguments
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);

        Assert.NotNull(response);
        Assert.Equal(400, response.Status);
        Assert.Contains("Missing required", response.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DatabaseListResult
    {
        [JsonPropertyName("databases")]
        public List<string>? Databases { get; set; }
    }
}
