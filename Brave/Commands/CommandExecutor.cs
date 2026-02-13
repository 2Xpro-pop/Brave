using Brave.Compile;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Windows.Input;

namespace Brave.Commands;

internal sealed class CommandExecutor : ICommand, IDisposable
{
    private readonly IAbstractResources _resources;
    private readonly ImmutableArray<CommandInstruction> _commandInstructions;
    private readonly IMetaInfoProvider _metaInfoProvider;

    private bool _canExecute = true;
    private readonly IObservable<bool>? _canExecuteObservable;
    private readonly IDisposable? _disposable;
    private readonly CanExecuteObserver? _canExecuteObserver;
    private readonly bool _hasReturn;

    internal CommandExecutor(IAbstractResources resources, IObservable<bool>? canExecute, ImmutableArray<CommandInstruction> commandInstructions, IMetaInfoProvider metaInfoProvider)
    {
        _resources = resources;
        _commandInstructions = commandInstructions;
        _metaInfoProvider = metaInfoProvider;

        _canExecuteObservable = canExecute;
        _canExecuteObserver = new CanExecuteObserver(this);
        _disposable = _canExecuteObservable?.Subscribe(_canExecuteObserver);
        _hasReturn = commandInstructions.Any(i =>
            (i.OpCode == CommandOpCode.SetResource || i.OpCode == CommandOpCode.DirectSetResource) &&
            i.Arguments.Count > 0 &&
            i.Arguments[0]?.ToString() == "$return"
        );
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute;

    public void Execute(object? parameter)
    {
        Interpretator.Execute(_resources, parameter, _metaInfoProvider, _commandInstructions);
    }

    public object? ExecuteWithReturn(object? parameter, out object? @return)
    {
        var result = Interpretator.Execute(_resources, parameter, _metaInfoProvider, _commandInstructions);

        if (_hasReturn)
        {
            @return = _resources.TryGetValue("$return", out var value) ? value : null;

            if(@return == RuntimeStack.Indexes.Last)
            {
                @return = result;
            }
        }
        else
        {
            @return = Interpretator.Void;
        }
        
        return result;
    }

    private sealed class CanExecuteObserver : IObserver<bool>
    {
        private readonly CommandExecutor _executor;

        public CanExecuteObserver(CommandExecutor executor)
        {
            _executor = executor;
        }

        public void OnNext(bool value)
        {
            _executor._canExecute = value;
            _executor.CanExecuteChanged?.Invoke(_executor, EventArgs.Empty);
        }

        public void OnError(Exception error) { }

        public void OnCompleted() { }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _disposable?.Dispose();
    }

    ~CommandExecutor()
    {
        Dispose();
    }
}
