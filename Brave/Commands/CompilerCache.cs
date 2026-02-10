using Brave.Collections;
using Brave.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Brave.Commands;

internal static class CompilerCache
{
    const int CacheSize = 16; // Keep this small
    const int CacheMask = CacheSize - 1;

    private static readonly Entry[] s_cache = new Entry[CacheSize];

    struct Entry
    {
        public int Hash;
        public bool UseDirectSetResource;
        public string Expression;
        public ImmutableArray<CommandInstruction> Instructions;
    }

    public static bool TryGet(string expression, bool useDirectSetResource, out ImmutableArray<CommandInstruction> instructions)
    {
        var hash = GetHashCode(expression, useDirectSetResource);
        
        var index = hash & CacheMask;
        var entry = s_cache[index];

        if (entry.Hash == hash && entry.UseDirectSetResource == useDirectSetResource && entry.Expression == expression)
        {
            instructions = entry.Instructions;
            return true;
        }
        
        instructions = default;
        return false;
    }

    public static void Add(string expression, bool useDirectSetResource, ImmutableArray<CommandInstruction> instructions)
    {
        var hash = GetHashCode(expression, useDirectSetResource);
        
        var index = hash & CacheMask;
        s_cache[index] = new Entry
        {
            Hash = hash,
            Expression = expression,
            Instructions = instructions,
            UseDirectSetResource = useDirectSetResource
        };
    }

    private static int GetHashCode(string expression, bool useDirectSetResource)
    {
        var hash = FvnHashCode.GetFNVHashCode(expression);

        unchecked
        {
            var x = (uint)hash;
            x ^= useDirectSetResource ? 0x9E3779B9u : 0u; 

            // MurmurHash3 fmix32 
            x ^= x >> 16;
            x *= 0x85EBCA6Bu;
            x ^= x >> 13;
            x *= 0xC2B2AE35u;
            x ^= x >> 16;

            return (int)x;
        }
    }
}
