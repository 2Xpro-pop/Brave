using Brave.Compile;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Windows.Input;

namespace Brave.Commands;

internal sealed class CommandExecutor : ICommand
{
    private readonly IAbstractResources _resources;
    private readonly ImmutableArray<CommandInstruction> _commandInstructions;
    private readonly IMetaInfoProvider _metaInfoProvider;

    internal CommandExecutor(IAbstractResources resources, ImmutableArray<CommandInstruction> commandInstructions, IMetaInfoProvider metaInfoProvider)
    {
        _resources = resources;
        _commandInstructions = commandInstructions;
        _metaInfoProvider = metaInfoProvider;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public void Execute(object? parameter)
    {
        Interpretator.Execute(_resources, parameter, _metaInfoProvider, _commandInstructions);
    }
}
