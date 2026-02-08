using Avalonia.Data.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Brave.Avalonia;

internal sealed class InstancedProperty : IInstancedProperty, INotifyPropertyChanged
{
    private readonly object _instance;
    private readonly IPropertyInfo _propertyInfo;

    public InstancedProperty(object instance, IPropertyInfo propertyInfo)
    {
        _instance = instance;
        _propertyInfo = propertyInfo;
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            if (_instance is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += value;
            }
        }

        remove
        {
            if (_instance is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged -= value;
            }
        }
    }

    public string Name => _propertyInfo.Name;

    public bool CanSet => _propertyInfo.CanSet;

    public bool CanGet => _propertyInfo.CanGet;

    public object? Get()
    {
        return _propertyInfo.Get(_instance);
    }

    public void Set(object? value)
    {
        _propertyInfo.Set(_instance, value);
    }
}
