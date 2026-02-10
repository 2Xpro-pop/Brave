using System;
using System.Collections.Generic;
using System.Text;

namespace Brave;

internal static class MetaInfoProviderExtensions
{
    extension(IMetaInfoProvider metaInfo)
    {
        public object? CurrentOrRootObject
        {
            get => metaInfo.CurrentObject ?? metaInfo.RootObject;
        }
    }

}
