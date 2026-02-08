using NSubstitute;
using System;
using System.Collections;
using System.Collections.Generic;
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
                // set_Item(object key, object value) => both are object, must use ArgAt
                var key = callInfo.ArgAt<object>(0);
                var value = callInfo.ArgAt<object?>(1);

                backingDictionary[key] = value;
            });

        resources
            .When(r => r.Add(Arg.Any<object>(), Arg.Any<object?>()))
            .Do(callInfo =>
            {
                // Add(object key, object? value) => still ambiguous via Arg<object>()
                var key = callInfo.ArgAt<object>(0);
                var value = callInfo.ArgAt<object?>(1);

                backingDictionary.Add(key, value);
            });

        resources.Remove(Arg.Any<object>())
            .Returns(callInfo =>
            {
                var key = callInfo.ArgAt<object>(0);
                return backingDictionary.Remove(key);
            });

        resources
            .When(r => r.Clear())
            .Do(_ => backingDictionary.Clear());

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
