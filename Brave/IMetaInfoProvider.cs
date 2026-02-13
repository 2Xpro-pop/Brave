using System;
using System.Collections.Generic;
using System.Text;

namespace Brave;

internal interface IMetaInfoProvider
{
    public object? RootObject
    {
        get;
    }

    public object? IntermediateRootObject
    {
        get;
    }

    public IAbstractResources? RootResources
    {
        get;
    }

    public IAbstractResources? IntermediateRootResources
    {
        get;
    }

    public object? CurrentObject
    {
        get;
    }

    public IXamlNamespaceInfoProvider XamlNamespaceInfoProvider
    {
        get;
    }
}
