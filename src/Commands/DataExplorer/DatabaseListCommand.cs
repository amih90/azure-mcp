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

public sealed class DatabaseListCommand : BaseDatabaseCommand<DatabaseListArguments>
{
    private readonly ILogger<DatabaseListCommand> _logger;

    public DatabaseListCommand(ILogger<DatabaseListCommand> logger) : base()
    {
        _logger = logger;
    }

    protected override string GetCommandName() => "list";

    protected override string GetCommandDescription() =>
        """
        List all databases in a Data Explorer cluster. This command retrieves all databases available in the specified cluster and subscription. Results include database names and are returned as a JSON array.
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
            var databases = await dataExplorerService.ListDatabases(
                args.Subscription!,
                args.ClusterUri!,
                args.Tenant,
                args.AuthMethod,
                args.RetryPolicy);

            context.Response.Results = databases?.Count > 0 ? new { databases } : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred listing databases. ClusterUri: {ClusterUri}.", args.ClusterUri);
            HandleException(context.Response, ex);
        }
        return context.Response;
    }
}
