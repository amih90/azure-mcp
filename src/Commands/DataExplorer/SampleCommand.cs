// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Arguments.DataExplorer;
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.CommandLine.Parsing;
using System.Text.Json;

namespace AzureMcp.Commands.DataExplorer;

public sealed class SampleCommand(ILogger<SampleCommand> logger) : BaseSampleCommand<SampleArguments>
{
    private readonly ILogger<SampleCommand> _logger = logger;

    protected override string GetCommandName() => "sample";

    protected override string GetCommandDescription() =>
        "Return a sample of rows from the specified table in an Azure Data Explorer (Kusto) table.";

    [McpServerTool(Destructive = false, ReadOnly = true)]
    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult)
    {
        var args = BindArguments(parseResult);
        try
        {
            if (!await ProcessArguments(context, args))
                return context.Response;

            var dataExplorerService = context.GetService<IDataExplorerService>();
            List<JsonDocument> results;
            var query = $"{args.Table} | sample {args.Limit}";

            if (UseClusterUri(args))
            {
                results = await dataExplorerService.QueryItems(
                    args.ClusterUri!,
                    args.Database!,
                    query,
                    args.Tenant,
                    args.AuthMethod,
                    args.RetryPolicy);
            }
            else
            {
                results = await dataExplorerService.QueryItems(
                    args.Subscription!,
                    args.ClusterName!,
                    args.Database!,
                    query,
                    args.Tenant,
                    args.AuthMethod,
                    args.RetryPolicy);
            }

            context.Response.Results = results?.Count > 0 ? results : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred sampling table. Table: {Table}.", args.Table);
            HandleException(context.Response, ex);
        }
        return context.Response;
    }
}