using AzureMcp.Models.Argument;
using AzureMcp.Arguments.DataExplorer;
using System.CommandLine;
using System.CommandLine.Parsing;
using AzureMcp.Services.Interfaces;
using AzureMcp.Models.Command;

namespace AzureMcp.Commands.DataExplorer;

public abstract class BaseClusterCommand<TArgs> : SubscriptionCommand<TArgs> where TArgs : BaseClusterArguments, new()
{
    protected readonly Option<string> _clusterOption = ArgumentDefinitions.DataExplorer.Cluster.ToOption();

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_clusterOption);
    }

    protected override void RegisterArguments()
    {
        base.RegisterArguments();
        AddArgument(CreateClusterArgument());
    }

    protected override TArgs BindArguments(ParseResult parseResult)
    {
        var args = base.BindArguments(parseResult);
        args.Cluster = parseResult.GetValueForOption(_clusterOption);
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

    protected ArgumentBuilder<TArgs> CreateClusterArgument() =>
        ArgumentBuilder<TArgs>
            .Create(ArgumentDefinitions.DataExplorer.Cluster.Name, ArgumentDefinitions.DataExplorer.Cluster.Description)
            .WithValueAccessor(args => args.Cluster ?? string.Empty)
            .WithSuggestedValuesLoader(async (context, args) =>
                await GetClusterOptions(context, args.Subscription ?? string.Empty))
            .WithIsRequired(ArgumentDefinitions.DataExplorer.Cluster.Required);
}
