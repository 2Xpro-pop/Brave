using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Brave;

internal interface IInstancedProperty: INotifyPropertyChanged
{
    public string Name
    {
        get;
    }

    public object? Get();

    public void Set(object? value);

    public bool CanSet
    {
        get;
    }

    public bool CanGet
    {
        get;
    }

}
