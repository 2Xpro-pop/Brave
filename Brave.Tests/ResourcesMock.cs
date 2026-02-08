using NSubstitute;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;

namespace Brave.Tests;

internal static class ResourcesMock
{
    public static (IAbstractResources Resources, Dictionary<object, object?> Backing) CreateResources(
        object? owner = null,
        IAbstractResources? parent = null)
    {
        var backingDictionary = new Dictionary<object, object?>();
        var resources = Substitute.For<IAbstractResources>();

        // key -> callbacks
        var keySubscriptions = new Dictionary<object, List<Action>>();

        void NotifyKey(object key)
        {
            if (keySubscriptions.TryGetValue(key, out var list))
            {
                var snapshot = list.ToArray();
                for (var i = 0; i < snapshot.Length; i++)
                {
                    snapshot[i]();
                }
            }

            resources.ResourceChanged += Raise.Event<Action>();
        }

        resources.TrySubscribeToKeyOrResource(Arg.Any<object>(), Arg.Any<Action>())
            .Returns(callInfo =>
            {
                var key = callInfo.ArgAt<object>(0);
                var onChanged = callInfo.ArgAt<Action>(1);

                if (!keySubscriptions.TryGetValue(key, out var list))
                {
                    list = new List<Action>(capacity: 2);
                    keySubscriptions[key] = list;
                }

                list.Add(onChanged);

                return Disposable.Create(() =>
                {
                    if (keySubscriptions.TryGetValue(key, out var l))
                    {
                        l.Remove(onChanged);
                        if (l.Count == 0)
                        {
                            keySubscriptions.Remove(key);
                        }
                    }
                });
            });

        resources.Owner.Returns(owner ?? new object());
        resources.Parent.Returns(parent);
        resources.IsReadOnly.Returns(false);

        resources.Count.Returns(_ => backingDictionary.Count);
        resources.Keys.Returns(_ => backingDictionary.Keys);
        resources.Values.Returns(_ => backingDictionary.Values);

        resources.ContainsKey(Arg.Any<object>())
            .Returns(callInfo =>
            {
                var key = callInfo.ArgAt<object>(0);
                return backingDictionary.ContainsKey(key);
            });

        resources.TryGetValue(Arg.Any<object>(), out Arg.Any<object?>())
            .Returns(callInfo =>
            {
                var key = callInfo.ArgAt<object>(0);
                var success = backingDictionary.TryGetValue(key, out var value);
                callInfo[1] = value;
                return success;
            });

        resources.TryGetResource(Arg.Any<object>(), out Arg.Any<object?>())
            .Returns(callInfo =>
            {
                var key = callInfo.ArgAt<object>(0);
                var success = backingDictionary.TryGetValue(key, out var value);
                callInfo[1] = value;
                return success;
            });

        resources[Arg.Any<object>()]
            .Returns(callInfo =>
            {
                var key = callInfo.ArgAt<object>(0);
                return backingDictionary[key];
            });

        resources
            .When(r => r[Arg.Any<object>()] = Arg.Any<object?>())
            .Do(callInfo =>
            {
                var key = callInfo.ArgAt<object>(0);
                var value = callInfo.ArgAt<object?>(1);

                backingDictionary[key] = value;
                NotifyKey(key);
            });

        resources
            .When(r => r.Add(Arg.Any<object>(), Arg.Any<object?>()))
            .Do(callInfo =>
            {
                var key = callInfo.ArgAt<object>(0);
                var value = callInfo.ArgAt<object?>(1);

                backingDictionary.Add(key, value);
                NotifyKey(key);
            });

        resources.Remove(Arg.Any<object>())
            .Returns(callInfo =>
            {
                var key = callInfo.ArgAt<object>(0);
                var removed = backingDictionary.Remove(key);

                if (removed)
                {
                    NotifyKey(key);
                }

                return removed;
            });

        resources
            .When(r => r.Clear())
            .Do(_ =>
            {
                backingDictionary.Clear();
                resources.ResourceChanged += Raise.Event<Action>();

                foreach (var kv in keySubscriptions)
                {
                    var snapshot = kv.Value.ToArray();
                    for (var i = 0; i < snapshot.Length; i++)
                    {
                        snapshot[i]();
                    }
                }
            });

        resources
            .When(r => r.CopyTo(Arg.Any<KeyValuePair<object, object?>[]>(), Arg.Any<int>()))
            .Do(callInfo =>
            {
                var array = callInfo.ArgAt<KeyValuePair<object, object?>[]>(0);
                var arrayIndex = callInfo.ArgAt<int>(1);

                ((IDictionary<object, object?>)backingDictionary).CopyTo(array, arrayIndex);
            });

        resources.GetEnumerator()
            .Returns(_ => ((IDictionary<object, object?>)backingDictionary).GetEnumerator());

        ((IEnumerable)resources).GetEnumerator()
            .Returns(_ => ((IEnumerable)backingDictionary).GetEnumerator());

        return (resources, backingDictionary);
    }
}
