// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Arguments.DataExplorer;
using AzureMcp.Models;
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.CommandLine.Parsing;

namespace AzureMcp.Commands.DataExplorer;

public sealed class ClusterListCommand : SubscriptionCommand<ClusterListArguments>
{
    private readonly ILogger<ClusterListCommand> _logger;

    public ClusterListCommand(ILogger<ClusterListCommand> logger) : base()
    {
        _logger = logger;
    }

    protected override string GetCommandName() => "list";

    protected override string GetCommandDescription() =>
        """
        List all Data Explorer clusters in a subscription. This command retrieves all clusters 
        available in the specified subscription. Results include cluster names and are returned as a JSON array.
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
            var clusters = await dataExplorerService.ListClusters(
                args.Subscription!,
                args.Tenant,
                args.RetryPolicy);

            context.Response.Results = clusters?.Count > 0 ? 
            new { clusters } : 
            null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Data Explorer clusters");
            HandleException(context.Response, ex);
        }
        return context.Response;
    }
}
