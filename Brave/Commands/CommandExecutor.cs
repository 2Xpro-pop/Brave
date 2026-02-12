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

    internal CommandExecutor(IAbstractResources resources, IObservable<bool>? canExecute, ImmutableArray<CommandInstruction> commandInstructions, IMetaInfoProvider metaInfoProvider)
    {
        _resources = resources;
        _commandInstructions = commandInstructions;
        _metaInfoProvider = metaInfoProvider;

        _canExecuteObservable = canExecute;
        _canExecuteObserver = new CanExecuteObserver(this);
        _disposable = _canExecuteObservable?.Subscribe(_canExecuteObserver);
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute;

    public void Execute(object? parameter)
    {
        Interpretator.Execute(_resources, parameter, _metaInfoProvider, _commandInstructions);
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
