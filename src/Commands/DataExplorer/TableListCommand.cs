// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Arguments.DataExplorer;
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.CommandLine.Parsing;

namespace AzureMcp.Commands.DataExplorer;

public sealed class TableListCommand(ILogger<TableListCommand> logger) : BaseDatabaseCommand<TableListArguments>
{
    private readonly ILogger<TableListCommand> _logger = logger;

    protected override string GetCommandName() => "list";

    protected override string GetCommandDescription() =>
        "List all tables in a specific Azure Data Explorer (Kusto) database.";

    [McpServerTool(Destructive = false, ReadOnly = true)]
    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult)
    {
        var args = BindArguments(parseResult);
        try
        {
            if (!await ProcessArguments(context, args))
                return context.Response;

            var dataExplorerService = context.GetService<IDataExplorerService>();
            List<string> tables = [];

            if (UseClusterUri(args))
            {
                tables = await dataExplorerService.ListTables(
                    args.ClusterUri!,
                    args.Database!,
                    args.Tenant,
                    args.AuthMethod,
                    args.RetryPolicy);
            }
            else
            {
                tables = await dataExplorerService.ListTables(
                    args.Subscription!,
                    args.ClusterName!,
                    args.Database!,
                    args.Tenant,
                    args.AuthMethod,
                    args.RetryPolicy);
            }

            context.Response.Results = tables?.Count > 0 ? new { tables } : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred listing tables. Database: {Database}.", args.Database);
            HandleException(context.Response, ex);
        }
        return context.Response;
    }
}
