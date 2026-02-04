using System;
using System.Collections.Generic;
using System.Text;

namespace Brave.Syntax;

internal class FvnHashCode
{
    /// <summary>
    /// The offset bias value used in the FNV-1a algorithm
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    public const int FnvOffsetBias = unchecked((int)2166136261);

    /// <summary>
    /// The generative factor used in the FNV-1a algorithm
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    public const int FnvPrime = 16777619;

    internal static int GetFNVHashCode(char ch)
    {
        return CombineFNVHash(FnvOffsetBias, ch);
    }

    internal static int GetFNVHashCode(byte[] data)
    {
        var hashCode = FnvOffsetBias;

        for (var i = 0; i < data.Length; i++)
        {
            hashCode = unchecked((hashCode ^ data[i]) * FnvPrime);
        }

        return hashCode;
    }


    public static int GetFNVHashCode(ReadOnlySpan<byte> data, out bool isAscii)
    {
        var hashCode = FnvOffsetBias;

        byte asciiMask = 0;

        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];
            asciiMask |= b;
            hashCode = unchecked((hashCode ^ b) * FnvPrime);
        }

        isAscii = (asciiMask & 0x80) == 0;
        return hashCode;
    }

    public static int GetCaseInsensitiveFNVHashCode(ReadOnlySpan<char> data)
    {
        static char ToLowerInvariant(char c)
        {
            if ((uint)(c - 65) <= 25u)
            {
                return (char)(c | 0x20);
            }

            return c;
        }

        var hashCode = FnvOffsetBias;

        for (var i = 0; i < data.Length; i++)
        {
            hashCode = unchecked((hashCode ^ ToLowerInvariant(data[i])) * FnvPrime);
        }

        return hashCode;
    }

    public static int GetFNVHashCode(ReadOnlySpan<char> data)
    {
        var hashCode = FnvOffsetBias;

        for (var i = 0; i < data.Length; i++)
        {
            hashCode = unchecked((hashCode ^ data[i]) * FnvPrime);
        }

        return hashCode;
    }

    internal static int CombineFNVHash(int hashCode, string text)
            => CombineFNVHash(hashCode, text.AsSpan());

    /// <summary>
    /// Combine a char with an existing FNV-1a hash code
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    /// <param name="hashCode">The accumulated hash code</param>
    /// <param name="ch">The new character to combine</param>
    /// <returns>The result of combining <paramref name="hashCode"/> with <paramref name="ch"/> using the FNV-1a algorithm</returns>
    internal static int CombineFNVHash(int hashCode, char ch)
    {
        return unchecked((hashCode ^ ch) * FnvPrime);
    }

    internal static int CombineFNVHash(int hashCode, ReadOnlySpan<char> data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            hashCode = unchecked((hashCode ^ data[i]) * FnvPrime);
        }

        return hashCode;
    }
}

