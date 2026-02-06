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

        public object? GetOrCreate(object key, object? defaultValue = null)
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

            resources[key] = defaultValue;
            return defaultValue;
        }
    }
}
