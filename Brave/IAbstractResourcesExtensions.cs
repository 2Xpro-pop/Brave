using System;
using System.Collections.Generic;
using System.Text;

namespace Brave;

internal static class IAbstractResourcesExtensions
{
    extension(IAbstractResources resources)
    {
        public bool TrySetToExistingKey(object key, object? value)
        {
            if (resources.ContainsKey(key))
            {
                resources[key] = value;
                return true;
            }

            var parent = resources.Parent;
            while (parent != null)
            {
                if (parent.ContainsKey(key))
                {
                    parent[key] = value;
                    return true;
                }

                parent = parent.Parent;
            }

            resources[key] = value; // Set to local resources if not found in any parent.
            return false;
        }

        public object? GetOrCreate(object key, object? defaultValue = null, IMetaInfoProvider? metaInfoProvider = null)
        {

            if (resources.TryGetValue(key, out object? value))
            {
                return value;
            }

            var parent = resources.Parent;
            while (parent != null)
            {
                if (parent.TryGetValue(key, out value))
                {
                    return value;
                }
                parent = parent.Parent;
            }
            
            if(metaInfoProvider is not null)
            {
                var intermediateRootResources = metaInfoProvider.IntermediateRootResources;
                if (intermediateRootResources != null && intermediateRootResources.TryGetValue(key, out value))
                {
                    return value;
                }

                var root = metaInfoProvider.RootResources;

                if(root != null && root.TryGetValue(key, out value))
                {
                    return value;
                }
            }

            resources[key] = defaultValue;
            return defaultValue;
        }
    }
}
