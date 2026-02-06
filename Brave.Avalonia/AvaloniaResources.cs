using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Brave.Avalonia;

public sealed class AvaloniaResources: IAbstractResources
{
    private readonly IResourceDictionary _resources;
    private readonly StyledElement _styledElement;

    public AvaloniaResources(StyledElement styledElement)
    {
        _styledElement = styledElement;
        _resources = styledElement.Resources;

        _resources.OwnerChanged += (s, e) => ResourceChanged?.Invoke();
    }

    public event Action? ResourceChanged;

    public IAbstractResources? Parent
    {
        get
        {
            if(field != null)
            {
                return field;
            }

            if(_styledElement is Visual visual)
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

    #region IDictionary Implementation

    public object? this[object key] 
    { 
        get => _resources[key];
        set => _resources[key] = value;
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

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object? value) =>
        _resources.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion
}
