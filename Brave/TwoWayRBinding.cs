using System;
using System.Collections.Generic;
using System.Text;

namespace Brave;

internal sealed class TwoWayRBinding : IObservable<object>, IDisposable
{
    private readonly List<IObserver<object>> _subscribers = [];

    private readonly object _key;
    private readonly IObservable<object> _source;
    private readonly IAbstractResources _resources;
    private readonly IDisposable _disposable;

    public TwoWayRBinding(object key, IObservable<object?> source, IAbstractResources resources)
    {
        _key = key;
        _source = source;
        _resources = resources;

        _disposable = _source.Subscribe(new ValueObserver(this));
    }

    public object? Value
    {
        get;
        set
        {
            var convertedValue = TargetConverter != null ? TargetConverter(value) : value;

            if (field == convertedValue)
            {
                return;
            }

            field = convertedValue;

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

    public IDisposable Subscribe(IObserver<object> observer)
    {
        _subscribers.Add(observer);
        return new Subscription(this, observer);
    }

    private void ValueChanged()
    {
        _resources.TrySetToExistingKey(_key, Value);

        for (var i = 0; i < _subscribers.Count; i++)
        {
            var observer = _subscribers[i];

            observer.OnNext(Value!);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _disposable.Dispose();

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
        private readonly IObserver<object> _observer;

        public Subscription(TwoWayRBinding binding, IObserver<object> observer)
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
            // Do nothing
        }
        public void OnError(Exception error)
        {
            // Do nothing
        }
        public void OnNext(object? value)
        {
            _binding.Value = value;
        }
    }
}
