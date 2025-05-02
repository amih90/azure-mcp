// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Arguments; // For RetryPolicyArguments
using AzureMcp.Commands.DataExplorer;
using AzureMcp.Models; // For AuthMethod
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.CommandLine.Parsing;
using Xunit;

namespace AzureMcp.Tests.Commands.DataExplorer;

public sealed class TableSchemaCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDataExplorerService _dataExplorerService;
    private readonly ILogger<TableSchemaCommand> _logger;

    public TableSchemaCommandTests()
    {
        _dataExplorerService = Substitute.For<IDataExplorerService>();
        _logger = Substitute.For<ILogger<TableSchemaCommand>>();
        var collection = new ServiceCollection();
        collection.AddSingleton(_dataExplorerService);
        _serviceProvider = collection.BuildServiceProvider();
    }

    public static IEnumerable<object[]> TableSchemaArgumentMatrix()
    {
        yield return new object[] { "--subscription sub1 --cluster-name mycluster --database-name db1 --table-name table1", false };
        yield return new object[] { "--cluster-uri https://mycluster.kusto.windows.net --database-name db1 --table-name table1", true };
    }

    [Theory]
    [MemberData(nameof(TableSchemaArgumentMatrix))]
    public async Task ExecuteAsync_ReturnsSchema(string cliArgs, bool useClusterUri)
    {
        var expectedSchema = new List<System.Text.Json.JsonDocument> {
            System.Text.Json.JsonDocument.Parse("{\"Name\":\"col1\",\"Type\":\"string\"}"),
            System.Text.Json.JsonDocument.Parse("{\"Name\":\"col2\",\"Type\":\"int\"}")
        };
        if (useClusterUri)
        {
            _dataExplorerService.GetTableSchema(
                "https://mycluster.kusto.windows.net",
                "db1",
                "table1",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(expectedSchema);
        }
        else
        {
            _dataExplorerService.GetTableSchema(
                "sub1", "mycluster", "db1", "table1",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(expectedSchema);
        }
        var command = new TableSchemaCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse(cliArgs);
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);
        Assert.NotNull(response);
        Assert.NotNull(response.Results);
        var schema = response.Results?.GetType().GetProperty("schema")?.GetValue(response.Results) as List<System.Text.Json.JsonDocument>;
        Assert.NotNull(schema);
        Assert.Equal(2, schema?.Count);
        Assert.Equal("col1", schema![0].RootElement.GetProperty("Name").GetString());
        Assert.Equal("col2", schema![1].RootElement.GetProperty("Name").GetString());
    }

    [Theory]
    [MemberData(nameof(TableSchemaArgumentMatrix))]
    public async Task ExecuteAsync_ReturnsNull_WhenNoSchema(string cliArgs, bool useClusterUri)
    {
        if (useClusterUri)
        {
            _dataExplorerService.GetTableSchema(
                "https://mycluster.kusto.windows.net",
                "db1",
                "table1",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(new List<System.Text.Json.JsonDocument>());
        }
        else
        {
            _dataExplorerService.GetTableSchema(
                "sub1", "mycluster", "db1", "table1",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(new List<System.Text.Json.JsonDocument>());
        }
        var command = new TableSchemaCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse(cliArgs);
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);
        Assert.NotNull(response);
        Assert.Null(response.Results);
    }

    [Theory]
    [MemberData(nameof(TableSchemaArgumentMatrix))]
    public async Task ExecuteAsync_HandlesException_AndSetsException(string cliArgs, bool useClusterUri)
    {
        var expectedError = "Test error. To mitigate this issue, please refer to the troubleshooting guidelines here at https://aka.ms/azmcp/troubleshooting.";
        if (useClusterUri)
        {
            _dataExplorerService.GetTableSchema(
                "https://mycluster.kusto.windows.net",
                "db1",
                "table1",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(Task.FromException<List<System.Text.Json.JsonDocument>>(new Exception("Test error")));
        }
        else
        {
            _dataExplorerService.GetTableSchema(
                "sub1", "mycluster", "db1", "table1",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(Task.FromException<List<System.Text.Json.JsonDocument>>(new Exception("Test error")));
        }
        var command = new TableSchemaCommand(_logger);
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
        var command = new TableSchemaCommand(_logger);
        var parser = new Parser(command.GetCommand());
        var args = parser.Parse("");
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);
        Assert.NotNull(response);
        Assert.Equal(400, response.Status);
    }
}