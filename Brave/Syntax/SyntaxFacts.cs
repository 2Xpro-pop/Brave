using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Brave.Syntax;

internal static class SyntaxFacts
{
    public static bool IsHexDigit(char c)
    {
        return (c >= '0' && c <= '9') ||
               (c >= 'A' && c <= 'F') ||
               (c >= 'a' && c <= 'f');
    }

    public static bool IsBinaryDigit(char c)
    {
        return c == '0' | c == '1';
    }

    public static bool IsDecDigit(char c)
    {
        return c >= '0' && c <= '9';
    }

    public static int HexValue(char c)
    {
        Debug.Assert(IsHexDigit(c));
        return (c >= '0' && c <= '9') ? c - '0' : (c & 0xdf) - 'A' + 10;
    }

    public static int BinaryValue(char c)
    {
        Debug.Assert(IsBinaryDigit(c));
        return c - '0';
    }

    public static int DecValue(char c)
    {
        Debug.Assert(IsDecDigit(c));
        return c - '0';
    }

    public static bool IsWhitespace(char ch)
    {
        return ch == ' '
            || ch == '\t'
            || ch == '\v'
            || ch == '\f'
            || ch == '\u00A0' // NO-BREAK SPACE
            || ch == '\uFEFF'
            || ch == '\u001A'
            || (ch > 255 && CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.SpaceSeparator);
    }

    public static bool IsNewLine(char ch)
    {
        return ch == '\r'
            || ch == '\n'
            || ch == '\u0085'
            || ch == '\u2028'
            || ch == '\u2029';
    }

    public static bool IsIdentifierPartCharacter(char ch)
    {
        return (ch >= 'A' && ch <= 'Z')
            || (ch >= 'a' && ch <= 'z')
            || ch == '_'
            || SyntaxFacts.IsDecDigit(ch);
    }
}
