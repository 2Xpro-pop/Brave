using Brave.Compile;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Windows.Input;

namespace Brave.Commands;

public sealed class CommandExecutor : ICommand
{
    private readonly IAbstractResources _resources;
    private readonly ImmutableArray<CommandInstruction> _commandInstructions;

    public CommandExecutor(IAbstractResources resources, ImmutableArray<CommandInstruction> commandInstructions)
    {
        _resources = resources;
        _commandInstructions = commandInstructions;
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
        Interpretator.Execute(_resources, parameter, _commandInstructions);
    }
}
