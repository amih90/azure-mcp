using AzureMcp.Arguments.DataExplorer;
using AzureMcp.Commands.Cosmos;
using AzureMcp.Models.Argument;
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace AzureMcp.Commands.DataExplorer;

public abstract class BaseDatabaseGlobalCommand<TArgs> : GlobalCommand<TArgs> where TArgs : BaseClusterGlobalArguments, new()
{
    protected readonly Option<string> _clusterUriOption = ArgumentDefinitions.DataExplorer.Uri.ToOption();

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_clusterUriOption);
    }

    protected override TArgs BindArguments(ParseResult parseResult)
    {
        var args = base.BindArguments(parseResult);
        args.ClusterUri = parseResult.GetValueForOption(_clusterUriOption);
        return args;
    }
}
