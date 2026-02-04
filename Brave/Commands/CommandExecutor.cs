using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace Brave.Commands;

internal class CommandExecutor : ICommand
{
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
        throw new NotImplementedException();
    }
}
