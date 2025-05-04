// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Arguments; // For RetryPolicyArguments
using AzureMcp.Commands.Kusto;
using AzureMcp.Models; // For AuthMethod
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.CommandLine.Parsing;
using System.Text.Json.Nodes;
using Xunit;

namespace AzureMcp.Tests.Commands.Kusto;

public sealed class TableSchemaCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IKustoService _kusto;
    private readonly ILogger<TableSchemaCommand> _logger;

    public TableSchemaCommandTests()
    {
        _kusto = Substitute.For<IKustoService>();
        _logger = Substitute.For<ILogger<TableSchemaCommand>>();
        var collection = new ServiceCollection();
        collection.AddSingleton(_kusto);
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
        var expectedSchema = new List<JsonNode> {
            JsonNode.Parse("{\"Name\":\"col1\",\"Type\":\"string\"}")!,
            JsonNode.Parse("{\"Name\":\"col2\",\"Type\":\"int\"}")!
        };
        if (useClusterUri)
        {
            _kusto.GetTableSchema(
                "https://mycluster.kusto.windows.net",
                "db1",
                "table1",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(expectedSchema);
        }
        else
        {
            _kusto.GetTableSchema(
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
        var json = System.Text.Json.JsonSerializer.Serialize(response.Results);
        var result = System.Text.Json.JsonSerializer.Deserialize<TableSchemaResult>(json);
        Assert.NotNull(result);
        Assert.NotNull(result.Schema);
        Assert.Equal(2, result.Schema.Count);
        Assert.Equal("col1", result.Schema[0]["Name"]?.ToString());
        Assert.Equal("col2", result.Schema[1]["Name"]?.ToString());
    }

    [Theory]
    [MemberData(nameof(TableSchemaArgumentMatrix))]
    public async Task ExecuteAsync_ReturnsNull_WhenNoSchema(string cliArgs, bool useClusterUri)
    {
        if (useClusterUri)
        {
            _kusto.GetTableSchema(
                "https://mycluster.kusto.windows.net",
                "db1",
                "table1",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(new List<JsonNode>());
        }
        else
        {
            _kusto.GetTableSchema(
                "sub1", "mycluster", "db1", "table1",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(new List<JsonNode>());
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
            _kusto.GetTableSchema(
                "https://mycluster.kusto.windows.net",
                "db1",
                "table1",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(Task.FromException<List<JsonNode>>(new Exception("Test error")));
        }
        else
        {
            _kusto.GetTableSchema(
                "sub1", "mycluster", "db1", "table1",
                Arg.Any<string>(), Arg.Any<AuthMethod?>(), Arg.Any<RetryPolicyArguments>())
                .Returns(Task.FromException<List<JsonNode>>(new Exception("Test error")));
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

    private sealed class TableSchemaResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("schema")]
        public List<JsonNode> Schema { get; set; } = new();
    }
}