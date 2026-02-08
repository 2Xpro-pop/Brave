namespace Brave;

public interface IAbstractResources: IDictionary<object, object?>
{
    public event Action? ResourceChanged;

    public IAbstractResources? Parent
    {
        get;
    }

    public object? Owner
    {
        get;
    }

    public bool TryGetResource(object key, out object? value);

    public IDisposable TrySubscribeToKeyOrResource(object key, Action onChanged);
}
