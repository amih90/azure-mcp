// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.Argument;
using AzureMcp.Arguments.DataExplorer;
using System.CommandLine;
using System.CommandLine.Parsing;
using AzureMcp.Services.Interfaces;
using AzureMcp.Models.Command;

namespace AzureMcp.Commands.DataExplorer;

public abstract class BaseClusterCommand<TArgs> : SubscriptionCommand<TArgs> where TArgs : BaseClusterArguments, new()
{
    protected readonly Option<string> _clusterNameOption = ArgumentDefinitions.DataExplorer.Cluster.ToOption();
    protected readonly Option<string> _clusterUriOption = ArgumentDefinitions.DataExplorer.ClusterUri.ToOption();

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_clusterUriOption);
        command.AddOption(_clusterNameOption);
    }

    protected override void RegisterArguments()
    {
        base.RegisterArguments();
        AddArgument(CreateClusterUriArgument());
        AddArgument(CreateClusterNameArgument());

        var command = GetCommand();
        command.AddValidator(result =>
        {
            var clusterUri = result.GetValueForOption(_clusterUriOption);
            var clusterName = result.GetValueForOption(_clusterNameOption);

            if (!string.IsNullOrEmpty(clusterUri))
            {
                // If clusterUri is provided, make subscription optional
                var subscriptionArgument = GetArguments()?.FirstOrDefault(arg => string.Equals(arg.Name, "subscription", StringComparison.OrdinalIgnoreCase));
                if (subscriptionArgument != null)
                {
                    subscriptionArgument.Required = false;
                }
            }
            else
            {
                var subscription = result.GetValueForOption(_subscriptionOption);

                // clusterUri not provided, require both subscription and clusterName
                if (string.IsNullOrEmpty(subscription) || string.IsNullOrEmpty(clusterName))
                {
                    result.ErrorMessage = $"Either --{_clusterUriOption.Name} must be provided, or both --{_subscriptionOption.Name} and --{_clusterNameOption.Name} must be provided.";
                }
            }
        });
    }

    protected override TArgs BindArguments(ParseResult parseResult)
    {
        var args = base.BindArguments(parseResult);
        args.ClusterUri = parseResult.GetValueForOption(_clusterUriOption);
        args.ClusterName = parseResult.GetValueForOption(_clusterNameOption);
        return args;
    }

    // Common method to get cluster options
    protected async Task<List<ArgumentOption>> GetClusterOptions(CommandContext context, string subscription)
    {
        if (string.IsNullOrEmpty(subscription)) return [];

        var dataExplorerService = context.GetService<IDataExplorerService>();
        var clusters = await dataExplorerService.ListClusters(subscription);

        return clusters?.Select(a => new ArgumentOption { Name = a, Id = a }).ToList() ?? [];
    }

    protected ArgumentBuilder<TArgs> CreateClusterUriArgument() =>
        ArgumentBuilder<TArgs>
            .Create(ArgumentDefinitions.DataExplorer.ClusterUri.Name, ArgumentDefinitions.DataExplorer.ClusterUri.Description)
            .WithValueAccessor(args => args.ClusterUri ?? string.Empty)
            .WithSuggestedValuesLoader(async (context, args) =>
                await GetClusterOptions(context, args.Subscription ?? string.Empty))
            .WithIsRequired(ArgumentDefinitions.DataExplorer.ClusterUri.Required);

    protected ArgumentBuilder<TArgs> CreateClusterNameArgument() =>
        ArgumentBuilder<TArgs>
            .Create(ArgumentDefinitions.DataExplorer.Cluster.Name, ArgumentDefinitions.DataExplorer.Cluster.Description)
            .WithValueAccessor(args => args.ClusterName ?? string.Empty)
            .WithSuggestedValuesLoader(async (context, args) =>
                await GetClusterOptions(context, args.Subscription ?? string.Empty))
            .WithIsRequired(ArgumentDefinitions.DataExplorer.Cluster.Required);
}
