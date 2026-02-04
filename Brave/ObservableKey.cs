using System;
using System.Collections.Generic;
using System.Text;

namespace Brave;

public sealed class ObservableKey
{
    private readonly object _key;
    private readonly IAbstractResources _resources;

    public ObservableKey(object key, IAbstractResources resources)
    {
        _key = key;
        _resources = resources;
    }

    public object? GetValue()
    {
        if (_resources.TryGetResource(_key, out var value))
        {
            return value!;
        }

        return null;
    }
}
