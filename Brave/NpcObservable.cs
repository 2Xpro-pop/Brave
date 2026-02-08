using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Brave;

internal sealed class NpcObservable: IObservable<object?>, IDisposable
{
    private readonly List<IObserver<object?>> _observers = [];

    private readonly IInstancedProperty _instancedProperty;

    public NpcObservable(IInstancedProperty instancedProperty)
    {
        _instancedProperty = instancedProperty;

        instancedProperty.PropertyChanged += OnPropertyChanged;
    }

    public IDisposable Subscribe(IObserver<object?> observer)
    {
        _observers.Add(observer);
        return new Subscription(this, observer);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName == _instancedProperty.Name)
        {
            Notify(_instancedProperty.Get());
        }
    }

    private void Notify(object? value)
    {
        for (var i = 0; i < _observers.Count; i++)
        {
            var observer = _observers[i];
            observer.OnNext(value);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _instancedProperty.PropertyChanged -= OnPropertyChanged;

        for (var i = 0; i < _observers.Count; i++)
        {
            var observer = _observers[i];
            observer.OnCompleted();
        }

        _observers.Clear();
    }

    ~NpcObservable()
    {
        Dispose();
    }

    private sealed class Subscription : IDisposable
    {
        private readonly NpcObservable _observable;
        private readonly IObserver<object?> _observer;
        public Subscription(NpcObservable observable, IObserver<object?> observer)
        {
            _observable = observable;
            _observer = observer;
        }
        public void Dispose()
        {
            _observable._observers.Remove(_observer);
        }
    }
}
