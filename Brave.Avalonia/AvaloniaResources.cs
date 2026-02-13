using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.XamlIl.Runtime;
using Avalonia.VisualTree;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Brave.Avalonia;

public sealed class AvaloniaResources : IAbstractResources
{
    public static AvaloniaResources GetFirstResources(IAvaloniaXamlIlParentStackProvider parentStackProvider, object owner)
    {
        if(owner is StyledElement styledElement)
        {
            return new AvaloniaResources(styledElement);
        }

        var parents = parentStackProvider.Parents.ToImmutableArray();

        for (int i = parents.Length - 1; i >= 0; i--)
        {
            if (parents[i] is StyledElement styled)
            {
                return new AvaloniaResources(styled);
            }
        }

        throw new NotSupportedException("No StyledElement found in the provided owner or parent stack to resolve Avalonia resources.");
    }

    private readonly IResourceDictionary _resources;
    private readonly StyledElement _styledElement;

    public AvaloniaResources(StyledElement styledElement)
    {
        _styledElement = styledElement;
        _resources = styledElement.Resources;

        _resources.OwnerChanged += (s, e) => ResourceChanged?.Invoke();
        styledElement.ResourcesChanged += (s, e) => ResourceChanged?.Invoke();
    }

    public event Action? ResourceChanged;

    public IAbstractResources? Parent
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            if (_styledElement is Visual visual)
            {
                var parent = visual.GetVisualParent() ?? visual.GetLogicalParent();

                if (parent is StyledElement styledParent)
                {
                    field = new AvaloniaResources(styledParent);
                    return field;
                }
            }

            return null;
        }
    }



    public StyledElement Owner => _styledElement;
    object? IAbstractResources.Owner => _styledElement;

    public bool TryGetResource(object key, out object? value)
    {
        return _resources.TryGetResource(key, null, out value);
    }

    public IDisposable TrySubscribeToKeyOrResource(object key, Action onChanged)
    {
        ResourceChanged += onChanged;
        return new Subscription(this, onChanged);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly AvaloniaResources _resources;
        private readonly Action _action;

        public Subscription(AvaloniaResources resources, Action action)
        {
            _resources = resources;
            _action = action;
        }

        public void Dispose()
        {
            _resources.ResourceChanged -= _action;
        }
    }

    #region IDictionary Implementation

    private object? _return;

    public object? this[object key]
    {
        get =>  key.Equals("$return") ? _return : _resources[key];
        set
        {
            if(key.Equals("$return"))
            {
                _return = value;
                return;
            }

            if (_resources.TryGetValue(key, out var existingValue) && Equals(existingValue, value))
            {
                return;
            }

            _resources[key] = value;
        }
    }

    public ICollection<object> Keys => _resources.Keys;

    public ICollection<object?> Values => _resources.Values;

    public int Count => _resources.Count;

    public bool IsReadOnly => false;

    public void Add(object key, object? value) =>
        _resources.Add(key, value);

    public void Add(KeyValuePair<object, object?> item) =>
        _resources.Add(item.Key, item.Value);

    public void Clear() =>
        _resources.Clear();

    public bool Contains(KeyValuePair<object, object?> item) =>
        ((IDictionary<object, object?>)_resources).Contains(item);

    public bool ContainsKey(object key) =>
        _resources.ContainsKey(key);

    public void CopyTo(KeyValuePair<object, object?>[] array, int arrayIndex) =>
        ((IDictionary<object, object?>)_resources).CopyTo(array, arrayIndex);

    public IEnumerator<KeyValuePair<object, object?>> GetEnumerator() =>
         _resources.GetEnumerator();

    public bool Remove(object key) =>
        _resources.Remove(key);

    public bool Remove(KeyValuePair<object, object?> item) =>
        ((IDictionary<object, object?>)_resources).Remove(item);

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object? value)
    {
        if(key.Equals("$return"))
        {
            value = _return;
            return true;
        }

        return _resources.TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    #endregion
}
