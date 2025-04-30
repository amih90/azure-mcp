using AzureMcp.Arguments.DataExplorer;
using AzureMcp.Commands.Cosmos;
using AzureMcp.Models.Argument;
using AzureMcp.Models.Command;
using AzureMcp.Services.Interfaces;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace AzureMcp.Commands.DataExplorer;

public abstract class BaseQueryCommand<TArgs> : BaseDatabaseCommand<TArgs> where TArgs : QueryArguments, new()
{
    protected readonly Option<string> _queryOption = ArgumentDefinitions.DataExplorer.Query.ToOption();


    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_queryOption);
    }

    protected override TArgs BindArguments(ParseResult parseResult)
    {
        var args = base.BindArguments(parseResult);
        args.Query = parseResult.GetValueForOption(_queryOption);
        return args;
    }
}
