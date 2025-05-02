// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Arguments.DataExplorer;
using AzureMcp.Models.Argument;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace AzureMcp.Commands.DataExplorer;

public abstract class BaseTableCommand<TArgs> : BaseDatabaseCommand<TArgs> where TArgs : BaseTableArguments, new()
{
    protected readonly Option<string> _tableOption = ArgumentDefinitions.DataExplorer.Table.ToOption();

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_tableOption);
    }

    protected override TArgs BindArguments(ParseResult parseResult)
    {
        var args = base.BindArguments(parseResult);
        args.Table = parseResult.GetValueForOption(_tableOption);
        return args;
    }
}
