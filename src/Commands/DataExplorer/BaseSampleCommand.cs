// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Arguments.DataExplorer;
using AzureMcp.Models.Argument;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace AzureMcp.Commands.DataExplorer;

public abstract class BaseSampleCommand<TArgs> : BaseTableCommand<TArgs> where TArgs : SampleArguments, new()
{
    protected readonly Option<int> _limitOption = ArgumentDefinitions.DataExplorer.Limit.ToOption();

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_limitOption);
    }

    protected override TArgs BindArguments(ParseResult parseResult)
    {
        var args = base.BindArguments(parseResult);
        args.Limit = parseResult.GetValueForOption(_limitOption);
        return args;
    }
}
