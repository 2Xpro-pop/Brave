using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Brave.Pools;

internal static class ObjectHashSetPool
{
    private static readonly ConcurrentQueue<HashSet<object>> s_pool = new();

    static ObjectHashSetPool()
    {
        s_pool.Enqueue(new HashSet<object>(16));
    }

    public static HashSet<object> Rent()
    {
        if (s_pool.TryDequeue(out var hashSet))
        {
            return hashSet;
        }

        return [];
    }

    public static void Return(HashSet<object> hashSet)
    {
        hashSet.Clear();

        if(hashSet.Count > 16)
        {
            return;
        }

        if(s_pool.Count > 3)
        {
            return;
        }

        s_pool.Enqueue(hashSet);
    }
}
