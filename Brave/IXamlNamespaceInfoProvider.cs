using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Brave;

internal interface IXamlNamespaceInfoProvider
{
    public IReadOnlyDictionary<string, ImmutableArray<XamlNamespaceInfo>> XamlNamespaces
    {
        get;
    }
}
