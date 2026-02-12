using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.XamlIl.Runtime;
using Brave.Commands;
using Brave.Compile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Text;
using System.Windows.Input;

namespace Brave.Avalonia;

public sealed class ExecuteExtension
{
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public ExecuteExtension()
    {
        Expression = null!;
    }

    public ExecuteExtension(string expression)
    {
        Expression = expression;
    }

    [ConstructorArgument("expression")]
    public string Expression
    {
        get; set;
    }

    public object? CanExecute
    {
        get; set;
    }

    public ICommand? ProvideValue(IServiceProvider serviceProvider)
    {
        var styled = TargetObjectFinder.Find(serviceProvider);

        if (Expression is null)
        {
            throw new InvalidOperationException("Expression cannot be null.");
        }

        var metaInfoProvider = new MetaInfoProvider(serviceProvider);

        var resources = new AvaloniaResources(styled);
        var canExecuteObservable = GetCanExecute(CanExecute, resources, metaInfoProvider);

        var instructions = Compiler.Compile(Expression, useDirectSetResource: false);

        return new CommandExecutor(resources, canExecuteObservable, instructions, metaInfoProvider);
    }

    private static IObservable<bool>? GetCanExecute(object? canExecute, IAbstractResources resources, IMetaInfoProvider metaInfoProvider)
    {
        if (canExecute == null)
        {
            return null;
        }

        if (canExecute is IBinding binding)
        {
            throw new NotSupportedException("Binding is not supported currently.");
        }

        if (canExecute is string canExecuteString)
        {
            return new ToBoolObservable(new ObservableExpression(resources, canExecuteString, metaInfoProvider));
        }

        if(canExecute is int canExecuteInt)
        {
            return SingleBoolObservable.From(canExecuteInt != 0);
        }

        if (canExecute is bool canExecuteBool)
        {
            return SingleBoolObservable.From(canExecuteBool);
        }

        throw new NotSupportedException($"The type of CanExecute property is not supported. Type: {canExecute.GetType()}");
    }

    private sealed class ToBoolObservable : IObservable<bool>, IObserver<object?>, IDisposable
    {
        private bool _value;
        private readonly List<IObserver<bool>> _observers = [];

        public ToBoolObservable(IObservable<object?> source)
        {
            source.Subscribe(this);
        }

        public IDisposable Subscribe(IObserver<bool> observer)
        {
            observer.OnNext(_value);
            _observers.Add(observer);

            return new Subscription(this, observer);
        }

        public void OnCompleted()
        {
            Dispose();
        }

        public void OnError(Exception error)
        {
            for (int i = 0; i < _observers.Count; i++)
            {
                var observer = _observers[i];
                observer.OnError(error);
            }
        }

        public void OnNext(object? value)
        {
            if (value is bool boolValue)
            {
                _value = boolValue;
            }

            if (value is null)
            {
                _value = false;
            }

            if (value is IConvertible convertible)
            {
                try
                {
                    _value = convertible.ToBoolean(null);
                }
                catch (FormatException)
                {
                    _value = false;
                }
            }

            for (int i = 0; i < _observers.Count; i++)
            {
                var observer = _observers[i];
                observer.OnNext(_value);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly ToBoolObservable _parent;
            private readonly IObserver<bool> _observer;
            public Subscription(ToBoolObservable parent, IObserver<bool> observer)
            {
                _parent = parent;
                _observer = observer;
            }
            public void Dispose()
            {
                _parent._observers.Remove(_observer);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            for (int i = 0; i < _observers.Count; i++)
            {
                var obsercer = _observers[i];
                obsercer.OnCompleted();
            }

            _observers.Clear();
        }

        ~ToBoolObservable()
        {
            Dispose();
        }
    }

    private sealed class SingleBoolObservable : IObservable<bool>
    {
        public static readonly SingleBoolObservable True = new SingleBoolObservable(true);
        public static readonly SingleBoolObservable False = new SingleBoolObservable(false);

        public static SingleBoolObservable From(bool value) => value ? True : False;

        private readonly bool _value;

        private SingleBoolObservable(bool value)
        {
            _value = value;
        }

        public IDisposable Subscribe(IObserver<bool> observer)
        {
            observer.OnNext(_value);
            return EmptyDisposable.Instance;
        }
    }
}
