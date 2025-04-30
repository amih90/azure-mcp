// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Arguments.DataExplorer;
using AzureMcp.Models.Argument;
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.CommandLine.Parsing;

namespace AzureMcp.Commands.DataExplorer;

public sealed class QueryGlobalCommand : BaseQueryGlobalCommand<QueryGlobalArguments>
{
    private readonly ILogger<QueryGlobalCommand> _logger;

    public QueryGlobalCommand(ILogger<QueryGlobalCommand> logger) : base()
    {
        _logger = logger;
    }

    protected override string GetCommandName() => "query-global";

    protected override string GetCommandDescription() =>
        """
        Execute a KQL against items in a Data Explorer cluster. Requires cluster-uri, database, and query. Results are returned as a JSON array of documents.
        """;

    [McpServerTool(Destructive = false, ReadOnly = true)]
    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult)
    {
        var args = BindArguments(parseResult);
        try
        {
            if (!await ProcessArguments(context, args))
                return context.Response;

            var dataExplorerService = context.GetService<IDataExplorerService>();
            var results = await dataExplorerService.QueryItems(
                args.ClusterUri!,
                args.Database!,
                args.Query!,
                args.Tenant,
                args.AuthMethod,
                args.RetryPolicy);

            context.Response.Results = results?.Count > 0 ?
                new { results } :
                null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred querying Data Explorer. ClusterUri: {ClusterUri}, Database: {Database}," 
            + " Query: {Query}", args.ClusterUri, args.Database, args.Query);
            HandleException(context.Response, ex);
        }
        return context.Response;
    }
}
