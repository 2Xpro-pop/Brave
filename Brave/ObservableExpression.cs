using Brave.Commands;
using Brave.Compile;
using Brave.Pools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Brave;


internal sealed class ObservableExpression : IObservable<object?>, IDisposable
{
    const int CacheSize = 16;
    const int CacheMask = CacheSize - 1;

    private static readonly CacheEntry[] s_watchingKeys = new CacheEntry[CacheSize];


    struct CacheEntry
    {
        public ImmutableArray<CommandInstruction> Instructions;
        public int Hash;
        public WatchingKeys WatchingKeys;
    }

    private readonly IAbstractResources _resources;
    private readonly List<IObserver<object?>> _observers = [];
    private readonly ImmutableArray<IDisposable> _disposables;
    private readonly ImmutableArray<CommandInstruction> _instructions;
    private readonly IMetaInfoProvider? _metaInfoProvider;
    private readonly WatchingKeys _wathcingKeys;

    public ObservableExpression(IAbstractResources resources, string expression, IMetaInfoProvider? metaInfoProvider = null) : this(resources, Compiler.Compile(expression), metaInfoProvider)
    {
    }

    public ObservableExpression(IAbstractResources resources, ImmutableArray<CommandInstruction> instructions, IMetaInfoProvider? metaInfoProvider = null) : this(resources, instructions, metaInfoProvider, GetWatchingKeys(instructions))
    {
    }

    public ObservableExpression(IAbstractResources resources, ImmutableArray<CommandInstruction> instructions, IMetaInfoProvider? metaInfoProvider, WatchingKeys wathcingKeys)
    {
        _resources = resources;
        _instructions = instructions;
        _metaInfoProvider = metaInfoProvider;
        _wathcingKeys = wathcingKeys;

        using var disposables = ImmutableArrayBuilder<IDisposable>.Rent();
        for (int i = 0; i < _wathcingKeys.Length; i++)
        {
            var key = _wathcingKeys[i];

            var disposable = _resources.TrySubscribeToKeyOrResource(key, Calculate);

            disposables.Add(disposable);
        }

        _disposables = disposables.ToImmutable();

        Calculate();
    }

    public object? Value
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Notify(value);
        }
    }

    public object? Parameter
    {
        get;
        set
        {
            field = value;
            Calculate();
        }
    }

    public Func<object?, object?>? TargetConverter
    {
        get;
        set
        {
            field = value;
            Calculate();
        }
    }

    private void Calculate()
    {
        Value = Interpretator.Execute(_resources, Parameter, _metaInfoProvider, _instructions);
    }

    private void Notify(object? value)
    {
        var converted = TargetConverter is not null ? TargetConverter(value) : value;

        foreach (var observer in _observers)
        {
            observer.OnNext(converted);
        }
    }

    public IDisposable Subscribe(IObserver<object?> observer)
    {
        _observers.Add(observer);
        var converted = TargetConverter is not null ? TargetConverter(Value) : Value;
        observer.OnNext(converted);
        return new Subscription(this, observer);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        for (int i = 0; i < _observers.Count; i++)
        {
            var observer = _observers[i];
            observer.OnCompleted();
        }

        _observers.Clear();

        for (int i = 0; i < _disposables.Length; i++)
        {
            var disposable = _disposables[i];
            disposable.Dispose();
        }
    }

    ~ObservableExpression()
    {
        Dispose();
    }

    private sealed class Subscription : IDisposable
    {
        private readonly ObservableExpression _observableExpression;
        private readonly IObserver<object?> _observer;
        public Subscription(ObservableExpression observableExpression, IObserver<object?> observer)
        {
            _observableExpression = observableExpression;
            _observer = observer;
        }
        public void Dispose()
        {
            _observableExpression._observers.Remove(_observer);
        }
    }

    private static WatchingKeys GetWatchingKeys(ImmutableArray<CommandInstruction> instructions)
    {
        var hash = instructions[0].GetHashCode();

        for(var i = 1; i < instructions.Length; i++)
        {
            hash ^= instructions[i].GetHashCode();
            hash += i;
        }

        var index = hash & CacheMask;
        var entry = s_watchingKeys[index];

        if(entry.Hash == hash && entry.Instructions.Equals(instructions))
        {
            return entry.WatchingKeys;
        }

        HashSet<object> builder = ObjectHashSetPool.Rent();
        try
        {
            for (int i = 0; i < instructions.Length; i++)
            {
                var instruction = instructions[i];

                if (instruction.OpCode == CommandOpCode.GetResource)
                {
                    builder.Add(instruction.Arguments[0]);
                }
            }

            WatchingKeys keys = new([.. builder]);

            s_watchingKeys[index] = new CacheEntry
            {
                Hash = hash,
                Instructions = instructions,
                WatchingKeys = keys
            };

            return keys;
        }
        finally
        {
            ObjectHashSetPool.Return(builder);
        }
    }

    public readonly struct WatchingKeys
    {
        private readonly object? _keys;

        public WatchingKeys(ImmutableArray<object> keys)
        {
            if(keys.Length == 1)
            {
                _keys = keys[0];
                return;
            }

            _keys = keys;
        }

        public object this[int index]
        {
            get
            {
                if(_keys == null)
                {
                    throw new IndexOutOfRangeException();
                }

                if(_keys is ImmutableArray<object> array)
                {
                    return array[index]!;
                }

                if(index == 0)
                {
                    return _keys!;
                }

                throw new IndexOutOfRangeException();
            }
        }

        public int Length
        {
            get
            {
                if(_keys == null)
                {
                    return 0;
                }

                if(_keys is ImmutableArray<object> list)
                {
                    return list.Length;
                }

                return 1;
            }
        }
    }
}
