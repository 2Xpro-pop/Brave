using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Brave;

internal sealed class TwoWayRBinding : IObservable<object?>, IDisposable
{
    private readonly List<IObserver<object?>> _subscribers = [];

    private readonly object _key;
    private readonly IObservable<object?> _source;
    private readonly IAbstractResources _resources;
    private readonly IDisposable _disposable1;
    private readonly IDisposable _disposable2;

    public TwoWayRBinding(object key, IObservable<object?> source, IAbstractResources resources)
    {
        _key = key;
        _source = source;
        _resources = resources;

        _disposable1 = _source.Subscribe(new ValueObserver(this));

        Value = _resources.GetOrCreate(key);

        _disposable2 = _resources.TrySubscribeToKeyOrResource(key, () =>
        {
            var value = _resources.GetOrCreate(key);

            Value = value;
        });
    }

    public object? Value
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;

            ValueChanged();
        }
    }

    public Func<object?, object?>? TargetConverter
    {
        get; set;
    }

    public Func<object?, object?>? SourceConverter
    {
        get; set;
    } 

    public IDisposable Subscribe(IObserver<object?> observer)
    {
        _subscribers.Add(observer);

        var converted = TargetConverter is not null ? TargetConverter(Value) : Value;

        observer.OnNext(converted);

        return new Subscription(this, observer);
    }

    private void ValueChanged()
    {
        _resources.TrySetToExistingKey(_key, Value);

        var convertedValue = TargetConverter is not null ? TargetConverter(Value) : Value;

        for (var i = 0; i < _subscribers.Count; i++)
        {
            var observer = _subscribers[i];

            observer.OnNext(convertedValue);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _disposable1.Dispose();
        _disposable2.Dispose();

        for (var i = 0; i < _subscribers.Count; i++)
        {
            var observer = _subscribers[i];

            observer.OnCompleted();
        }

        _subscribers.Clear();
    }

    ~TwoWayRBinding()
    {
        Dispose();
    }

    private sealed class Subscription : IDisposable
    {
        private readonly TwoWayRBinding _binding;
        private readonly IObserver<object?> _observer;

        public Subscription(TwoWayRBinding binding, IObserver<object?> observer)
        {
            _binding = binding;
            _observer = observer;
        }

        public void Dispose()
        {
            _binding._subscribers.Remove(_observer);
        }
    }

    private sealed class ValueObserver : IObserver<object?>
    {
        private readonly TwoWayRBinding _binding;
        public ValueObserver(TwoWayRBinding binding)
        {
            _binding = binding;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(object? value)
        {
            if(value == BraveConstants.UnsetValue)
            {
                return;
            }

            object? converted = null!;

            if(_binding.SourceConverter is null)
            {
                var valueType = _binding.Value?.GetType();

                if(valueType is not null)
                {
                    converted = BraveConstants.FrameworkConverter?.Invoke(value, valueType) ?? value;

                    if(converted?.GetType().IsAssignableTo(valueType) != true)
                    {
                        converted = _binding.Value;
                    }
                }
                else
                {
                    converted = value;
                }
            }
            else
            {
                converted = _binding.SourceConverter(value);
            }

            _binding.Value = converted;
        }
    }
}
