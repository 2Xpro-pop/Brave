using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Brave.Syntax;

partial class Lexer
{
    public SyntaxToken? NextToken()
    {
        SkipWhitespace();

        if (IsAtEnd())
        {
            return null;
        }

        var character = PeekChar();

        switch (character)
        {
            case '+':
                AdvanceChar();

                if (TryAdvance('+'))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.PlusPlusToken);
                }

                if (TryAdvance('='))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.PlusEqualsToken);
                }

                return SyntaxToken.CreateToken(SyntaxKind.PlusToken);

            case '-':
                AdvanceChar();

                if (TryAdvance('-'))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.MinusMinusToken);
                }

                if (TryAdvance('='))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.MinusEqualsToken);
                }

                return SyntaxToken.CreateToken(SyntaxKind.MinusToken);

            case '*':
                AdvanceChar();

                if (TryAdvance('='))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.AsteriskEqualsToken);
                }

                return SyntaxToken.CreateToken(SyntaxKind.AsteriskToken);

            case '/':
                AdvanceChar();

                if (TryAdvance('='))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.SlashEqualsToken);
                }

                return SyntaxToken.CreateToken(SyntaxKind.SlashToken);

            case '&':
                AdvanceChar();

                if (TryAdvance('&'))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.AmpersandAmpersandToken);
                }

                if (TryAdvance('='))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.AmpersandEqualsToken);
                }

                return SyntaxToken.CreateToken(SyntaxKind.AmpersandToken);

            case '|':
                AdvanceChar();

                if (TryAdvance('|'))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.BarBarToken);
                }

                if (TryAdvance('='))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.BarEqualsToken);
                }

                return SyntaxToken.CreateToken(SyntaxKind.BarToken);

            case '<':
                AdvanceChar();

                if (TryAdvance('<'))
                {
                    if (TryAdvance('='))
                    {
                        return SyntaxToken.CreateToken(SyntaxKind.LessLessEqualsToken);
                    }

                    return SyntaxToken.CreateToken(SyntaxKind.LessLessToken);
                }

                if (TryAdvance('='))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.LessEqualsToken);
                }

                return SyntaxToken.CreateToken(SyntaxKind.LessToken);

            case '>':
                AdvanceChar();

                if (TryAdvance('>'))
                {
                    if (TryAdvance('='))
                    {
                        return SyntaxToken.CreateToken(SyntaxKind.GreaterGreaterEqualsToken);
                    }

                    return SyntaxToken.CreateToken(SyntaxKind.GreaterGreaterToken);
                }

                if (TryAdvance('='))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.GreaterEqualsToken);
                }

                return SyntaxToken.CreateToken(SyntaxKind.GreaterToken);

            case '?':
                AdvanceChar();

                if (TryAdvance('?'))
                {
                    if (TryAdvance('='))
                    {
                        return SyntaxToken.CreateToken(SyntaxKind.QuestionQuestionEqualsToken);
                    }

                    return SyntaxToken.CreateToken(SyntaxKind.QuestionQuestionToken);
                }

                return SyntaxToken.CreateToken(SyntaxKind.QuestionToken);

            case ':':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.ColonToken);

            case ',':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.CommaToken);

            case '.':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.DotToken);

            case '(':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.OpenParenToken);

            case ')':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.CloseParenToken);

            case ';':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.SemicolonToken);

            case '=':
                AdvanceChar();

                if (TryAdvance('='))
                {
                    return SyntaxToken.CreateToken(SyntaxKind.EqualEqualToken);
                }

                return SyntaxToken.CreateToken(SyntaxKind.EqualsToken);

            case '$':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.DollarToken);

            case '!':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.BangToken);

            case '[':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.OpenBracketToken);

            case ']':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.CloseBracketToken);

            case '{':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.OpenBraceToken);

            case '}':
                AdvanceChar();
                return SyntaxToken.CreateToken(SyntaxKind.CloseBraceToken);

            case '@':
                if (PeekChar(1) is '"' or '\'')
                {
                    return ParseString();
                }

                if(IsIdentifierTerminator(PeekChar()))
                {
                    AdvanceChar();
                    return SyntaxToken.CreateToken(SyntaxKind.AtToken);
                }

                return ScanIdentifier();

            case '\'':
                return ParseString();

            case '\"':
                return ParseString();

            case >= '0' and <= '9':
                return ScanNumericLiteral();
        }

        if (TryAdvanceKeyword("parameter"))
        {
            return SyntaxToken.CreateToken(SyntaxKind.ParameterKeyword);
        }

        return ScanIdentifier();
    }

    private bool TryAdvanceKeyword(string keyword)
    {
        ArgumentNullException.ThrowIfNull(keyword);

        if (keyword.Length == 0)
        {
            return false;
        }

        // Fast length check
        if (_position + keyword.Length > _length)
        {
            return false;
        }

        // Match keyword chars
        for (var index = 0; index < keyword.Length; index++)
        {
            if (_buffer[_position + index] != keyword[index])
            {
                return false;
            }
        }

        // Boundary check: next char must NOT continue an identifier
        var nextChar = (_position + keyword.Length) < _length
            ? _buffer[_position + keyword.Length]
            : '\0';

        if (!IsIdentifierTerminator(nextChar))
        {
            return false;
        }

        AdvanceChar(keyword.Length);
        return true;
    }

    #region Numeric Literal

    private SyntaxToken ScanNumericLiteral()
    {
        var startPosition = _position;

        var isHexadecimal = false;
        var isBinary = false;
        var hasDecimalPoint = false;
        var hasExponent = false;

        var hasUnsignedSuffix = false;
        var hasLongSuffix = false;

        // We build "value text" without underscores, similar to Roslyn.
        var builder = _stringBuilder.Builder;
        builder.Clear();

        // Prefix: 0x / 0b
        if (PeekChar() == '0')
        {
            var prefixChar = PeekChar(1);

            if (prefixChar is 'x' or 'X')
            {
                AdvanceChar(2);
                isHexadecimal = true;
            }
            else if (prefixChar is 'b' or 'B')
            {
                AdvanceChar(2);
                isBinary = true;
            }
        }

        if (isHexadecimal || isBinary)
        {
            ScanNumericLiteralSingleInteger(builder, isHexadecimal, isBinary);
            ScanIntegerSuffix(ref hasUnsignedSuffix, ref hasLongSuffix);
        }
        else
        {
            ScanNumericLiteralSingleInteger(builder, isHex: false, isBinary: false);

            // Decimal part: "." + digit
            if (PeekChar() == '.' && SyntaxFacts.IsDecDigit(PeekChar(1)))
            {
                hasDecimalPoint = true;
                builder.Append('.');
                AdvanceChar();
                ScanNumericLiteralSingleInteger(builder, isHex: false, isBinary: false);
            }

            // Exponent: e/E [+/-] digits
            if (PeekChar() is 'e' or 'E')
            {
                hasExponent = true;

                builder.Append(PeekChar());
                AdvanceChar();

                if (PeekChar() is '+' or '-')
                {
                    builder.Append(PeekChar());
                    AdvanceChar();
                }

                if (!IsDecDigitOrUnderscore(PeekChar()))
                {
                    // No diagnostics system in your snippet; keep parser stable.
                    builder.Append('0');
                }
                else
                {
                    ScanNumericLiteralSingleInteger(builder, isHex: false, isBinary: false);
                }
            }

            // Real suffixes
            var suffixChar = PeekChar();

            if (hasDecimalPoint || hasExponent)
            {
                if (suffixChar is 'f' or 'F')
                {
                    AdvanceChar();
                    return CreateNumericToken(startPosition, ParseSingle(builder));
                }

                if (suffixChar is 'd' or 'D')
                {
                    AdvanceChar();
                    return CreateNumericToken(startPosition, ParseDouble(builder));
                }

                if (suffixChar is 'm' or 'M')
                {
                    AdvanceChar();
                    return CreateNumericToken(startPosition, ParseDecimal(builder));
                }

                // Default real type
                return CreateNumericToken(startPosition, ParseDouble(builder));
            }

            // Integer literals can still have f/d/m (e.g. 1f) like in C#
            if (suffixChar is 'f' or 'F')
            {
                AdvanceChar();
                return CreateNumericToken(startPosition, ParseSingle(builder));
            }

            if (suffixChar is 'd' or 'D')
            {
                AdvanceChar();
                return CreateNumericToken(startPosition, ParseDouble(builder));
            }

            if (suffixChar is 'm' or 'M')
            {
                AdvanceChar();
                return CreateNumericToken(startPosition, ParseDecimal(builder));
            }

            // Integer suffixes: U/L/UL
            ScanIntegerSuffix(ref hasUnsignedSuffix, ref hasLongSuffix);
        }

        // Integer value selection
        var integerValue = ParseUInt64(builder, isHexadecimal, isBinary);
        var boxedValue = BoxIntegerLiteral(integerValue, hasUnsignedSuffix, hasLongSuffix);

        return CreateNumericToken(startPosition, boxedValue);
    }

    private SyntaxToken CreateNumericToken(int startPosition, object value)
    {
        var tokenText = Intern(startPosition, _position - startPosition);

        // If your factory signature differs, change ONLY this line.
        return SyntaxToken.CreateLiteral(SyntaxKind.NumericLiteralToken, tokenText, value);
    }

    private static object BoxIntegerLiteral(ulong value, bool hasUnsignedSuffix, bool hasLongSuffix)
    {
        // C# rules:
        // no suffix: int, uint, long, ulong
        // U: uint, ulong
        // L: long, ulong
        // UL: ulong

        if (!hasUnsignedSuffix && !hasLongSuffix)
        {
            if (value <= int.MaxValue)
            {
                return value switch
                {
                    0 => Boxes.BoxedInt0,
                    1 => Boxes.BoxedInt1,
                    _ => (int)value
                };
            }

            if (value <= uint.MaxValue)
            {
                return (uint)value;
            }

            if (value <= long.MaxValue)
            {
                return (long)value;
            }

            return value; // ulong
        }

        if (hasUnsignedSuffix && !hasLongSuffix)
        {
            return value <= uint.MaxValue ? (object)(uint)value : value;
        }

        if (!hasUnsignedSuffix && hasLongSuffix)
        {
            return value <= long.MaxValue ? (object)(long)value : value;
        }

        return value; // UL => ulong
    }

    // Allows '_' between digits; does not append '_' into parsing buffer.
    private void ScanNumericLiteralSingleInteger(StringBuilder builder, bool isHex, bool isBinary)
    {
        while (true)
        {
            var currentChar = PeekChar();

            if (currentChar == '_')
            {
                AdvanceChar();
                continue;
            }

            var isDigit =
                isHex ? SyntaxFacts.IsHexDigit(currentChar) :
                isBinary ? SyntaxFacts.IsBinaryDigit(currentChar) :
                SyntaxFacts.IsDecDigit(currentChar);

            if (!isDigit)
            {
                break;
            }

            builder.Append(currentChar);
            AdvanceChar();
        }
    }

    private void ScanIntegerSuffix(ref bool hasUnsignedSuffix, ref bool hasLongSuffix)
    {
        var suffixChar = PeekChar();

        if (suffixChar is 'l' or 'L')
        {
            AdvanceChar();
            hasLongSuffix = true;

            if (PeekChar() is 'u' or 'U')
            {
                AdvanceChar();
                hasUnsignedSuffix = true;
            }

            return;
        }

        if (suffixChar is 'u' or 'U')
        {
            AdvanceChar();
            hasUnsignedSuffix = true;

            if (PeekChar() is 'l' or 'L')
            {
                AdvanceChar();
                hasLongSuffix = true;
            }
        }
    }

    private static bool IsDecDigitOrUnderscore(char ch)
        => ch == '_' || SyntaxFacts.IsDecDigit(ch);

    private static float ParseSingle(StringBuilder builder)
        => float.TryParse(builder.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;

    private static double ParseDouble(StringBuilder builder)
        => double.TryParse(builder.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;

    private static decimal ParseDecimal(StringBuilder builder)
    {
        var style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
        return decimal.TryParse(builder.ToString(), style, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }

    private static ulong ParseUInt64(StringBuilder builder, bool isHex, bool isBinary)
    {
        ulong value;
        var text = builder.ToString();
        if (text.Length == 0)
        {
            return 0;
        }

        if (isBinary)
        {
            return TryParseBinaryUInt64(text, out value) ? value : 0;
        }

        var style = isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
        return ulong.TryParse(text, style, CultureInfo.InvariantCulture, out value) ? value : 0;
    }

    private static bool TryParseBinaryUInt64(string text, out ulong value)
    {
        value = 0;

        foreach (var character in text)
        {
            // if uppermost bit is set, then the next bitshift will overflow
            if ((value & 0x8000_0000_0000_0000) != 0)
            {
                return false;
            }

            var bit = (ulong)SyntaxFacts.BinaryValue(character);
            value = (value << 1) | bit;
        }

        return true;
    }

    #endregion

    #region String Literal

    private SyntaxToken ParseString()
    {
        var tokenStartPosition = _position;

        var builder = _stringBuilder.Builder;
        builder.Clear();

        var isVerbatim = false;
        char delimiter;

        // Detect @"..." or @'...'
        if (PeekChar() == '@' && PeekChar(1) is '"' or '\'')
        {
            isVerbatim = true;
            delimiter = PeekChar(1);
            AdvanceChar(2); // consume @"
        }
        else
        {
            delimiter = PeekChar(); // ' or "
            Debug.Assert(delimiter is '"' or '\'');
            AdvanceChar(); // consume opening quote
        }

        if (isVerbatim)
        {
            while (true)
            {
                var currentChar = PeekChar();

                // One-line constraint: stop on newline or end
                if (currentChar == '\0' || SyntaxFacts.IsNewLine(currentChar))
                {
                    break;
                }

                if (currentChar == delimiter)
                {
                    // doubled delimiter inside verbatim means one delimiter char
                    if (PeekChar(1) == delimiter)
                    {
                        builder.Append(delimiter);
                        AdvanceChar(2);
                        continue;
                    }

                    // closing delimiter
                    AdvanceChar();
                    break;
                }

                builder.Append(currentChar);
                AdvanceChar();
            }

            return CreateStringToken(tokenStartPosition, builder);
        }

        // Normal "..." / '...'
        while (true)
        {
            var currentChar = PeekChar();

            // One-line constraint: stop on newline or end
            if (currentChar == '\0' || SyntaxFacts.IsNewLine(currentChar))
            {
                break;
            }

            // closing quote
            if (currentChar == delimiter)
            {
                AdvanceChar();
                break;
            }

            if (currentChar == '\\')
            {
                // Try \uXXXX / \UXXXXXXXX (your existing helper)
                if (TryConsumeUnicodeEscape(out var decoded, out var surrogate))
                {
                    builder.Append(decoded);
                    if (surrogate != '\0')
                    {
                        builder.Append(surrogate);
                    }
                    continue;
                }

                // Simple escape: \n, \t, \\, \", \'
                AdvanceChar(); // consume '\'

                var escapeChar = PeekChar();
                if (escapeChar == '\0' || SyntaxFacts.IsNewLine(escapeChar))
                {
                    break;
                }

                builder.Append(DecodeSimpleEscape(escapeChar));
                AdvanceChar();
                continue;
            }

            builder.Append(currentChar);
            AdvanceChar();
        }

        return CreateStringToken(tokenStartPosition, builder);
    }

    private static char DecodeSimpleEscape(char escapeChar)
    {
        return escapeChar switch
        {
            '"' => '"',
            '\'' => '\'',
            '\\' => '\\',
            '0' => '\0',
            'a' => '\a',
            'b' => '\b',
            'f' => '\f',
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'v' => '\v',
            _ => escapeChar, // unknown escape => keep char
        };
    }

    private SyntaxToken CreateStringToken(int tokenStartPosition, StringBuilder builder)
    {
        var tokenText = Intern(tokenStartPosition, _position - tokenStartPosition);

        // Value = decoded string, intern to reduce allocations (like your numeric path)
        var valueText = Intern(builder.ToString());

        return SyntaxToken.CreateLiteral(SyntaxKind.StringLiteralToken, tokenText, valueText);
    }
    #endregion

    #region Identifier

    private SyntaxToken ScanIdentifier()
    {
        var startPosition = _position;

        if (TryParseIdentifierFast(out var identifierText))
        {
            return SyntaxToken.CreateIdentifier(identifierText);
        }

        Reset(startPosition);

        var slowText = ParseIdentifierSlow(startPosition);
        return SyntaxToken.CreateIdentifier(slowText);
    }

    /// <summary>
    /// Fast path for simple ASCII identifiers: [_a-zA-Z][_a-zA-Z0-9]*
    /// Returns false for non-ASCII or escapes.
    /// </summary>
    private bool TryParseIdentifierFast(out string identifierText)
    {
        identifierText = null!;

        var start = _position;
        if (start >= _length)
        {
            return false;
        }

        var firstChar = _buffer[start];

        // No fast path for verbatim or escapes or non-ASCII.
        if (firstChar == '@' || firstChar == '\\' || firstChar > 127)
        {
            return false;
        }

        if (!IsAsciiIdentifierStart(firstChar))
        {
            return false;
        }

        var index = start + 1;

        while (true)
        {
            if (index >= _length)
            {
                // Reached end => identifier ends here.
                var lengthAtEnd = _length - start;
                AdvanceChar(lengthAtEnd);
                identifierText = Intern(start, lengthAtEnd);
                return true;
            }

            var ch = _buffer[index];

            if (ch > 127 || ch == '\\')
            {
                // Needs slow path (unicode or escape).
                return false;
            }

            if (IsAsciiIdentifierPart(ch))
            {
                index++;
                continue;
            }

            // Terminator or anything non-identifier => stop
            break;
        }

        var length = index - start;
        AdvanceChar(length);
        identifierText = Intern(start, length);
        return true;
    }

    /// <summary>
    /// Slow path: supports '@' prefix, unicode identifier chars, and \uXXXX/\UXXXXXXXX escapes.
    /// Returns identifier name text (without '@').
    /// </summary>
    private string ParseIdentifierSlow(int tokenStartPosition)
    {
        ResetIdentifierBuffer();

        // Consume one or more '@'
        while (PeekChar() == '@')
        {
            AdvanceChar();
        }

        var identifierStartPosition = _position;
        var hasEscape = false;

        while (true)
        {
            var ch = PeekChar();

            // End
            if (ch == '\0')
            {
                break;
            }

            // Stop on whitespace/newline
            if (SyntaxFacts.IsWhitespace(ch) || SyntaxFacts.IsNewLine(ch))
            {
                break;
            }

            // Stop on common terminators (Akbura-style)
            if (IsIdentifierTerminator(ch))
            {
                break;
            }

            // Unicode escape inside identifier
            if (ch == '\\' && TryConsumeUnicodeEscape(out var decoded, out var surrogate))
            {
                hasEscape = true;

                // Validate start/part rules using decoded char
                if (_identifierLength == 0)
                {
                    if (!IsValidIdentifierStart(decoded))
                    {
                        break;
                    }
                }
                else
                {
                    if (!IsValidIdentifierPart(decoded))
                    {
                        break;
                    }
                }

                AddIdentifierChar(decoded);

                if (surrogate != '\0')
                {
                    AddIdentifierChar(surrogate);
                }

                continue;
            }

            // Validate start/part rules
            if (_identifierLength == 0)
            {
                if (!IsValidIdentifierStart(ch))
                {
                    break;
                }
            }
            else
            {
                if (!IsValidIdentifierPart(ch))
                {
                    break;
                }
            }

            // Ignore formatting chars (optional Roslyn-like behavior)
            if (ch > 127 && IsFormattingChar(ch))
            {
                AdvanceChar();
                continue;
            }

            AddIdentifierChar(ch);
            AdvanceChar();
        }

        // If nothing consumed as identifier => make progress by consuming 1 char (fallback)
        if (_identifierLength == 0)
        {
            Reset(tokenStartPosition);

            if (PeekChar() == '\0')
            {
                return Intern(string.Empty);
            }

            AdvanceChar();
            return Intern(tokenStartPosition, 1);
        }

        // If there were no escapes, we can intern directly from the source buffer,
        // excluding leading '@' (identifierStartPosition is after '@').
        if (!hasEscape)
        {
            return Intern(identifierStartPosition, _position - identifierStartPosition);
        }

        // With escapes: intern processed buffer (decoded)
        return Intern(_identifierBuffer, 0, _identifierLength);
    }

    private static bool IsAsciiIdentifierStart(char ch)
    {
        return ch == '_'
            || (ch >= 'a' && ch <= 'z')
            || (ch >= 'A' && ch <= 'Z');
    }

    private static bool IsAsciiIdentifierPart(char ch)
    {
        return IsAsciiIdentifierStart(ch)
            || (ch >= '0' && ch <= '9');
    }

    private static bool IsValidIdentifierStart(char ch)
    {
        if (ch <= 127)
        {
            return IsAsciiIdentifierStart(ch);
        }

        return true;
    }

    private static bool IsValidIdentifierPart(char ch)
    {
        if (ch <= 127)
        {
            return IsAsciiIdentifierPart(ch);
        }

        return true;
    }

    private static bool IsFormattingChar(char ch)
    {
        // Minimal replacement for UnicodeCharacterUtilities.IsFormattingChar
        return CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.Format;
    }

    private static bool IsIdentifierTerminator(char ch)
    {
        return ch switch
        {
            '\0' or ' ' or '\r' or '\n' or '\t'
            or '!' or '%' or '(' or ')' or '*' or '+' or ',' or '-'
            or '.' or '/' or ':' or ';' or '<' or '=' or '>' or '?'
            or '[' or ']' or '^' or '{' or '|' or '}' or '~'
            or '"' or '\'' or '&' => true,
            _ => false
        };
    }

    private void ResetIdentifierBuffer()
    {
        _identifierLength = 0;
    }

    private void AddIdentifierChar(char ch)
    {
        if (_identifierLength >= _identifierBuffer.Length)
        {
            GrowIdentifierBuffer();
        }

        _identifierBuffer[_identifierLength++] = ch;
    }

    private void GrowIdentifierBuffer()
    {
        var tmp = new char[_identifierBuffer.Length * 2];
        Array.Copy(_identifierBuffer, tmp, _identifierBuffer.Length);

        FreeIdentifierBuffer();

        _identifierBuffer = tmp;
    }

    /// <summary>
    /// Consumes \uXXXX or \UXXXXXXXX at current position (which must be '\').
    /// On success advances _position past the escape.
    /// </summary>
    private bool TryConsumeUnicodeEscape(out char decoded, out char surrogate)
    {
        decoded = '\0';
        surrogate = '\0';

        var start = _position;

        if (PeekChar() != '\\')
        {
            return false;
        }

        var kind = PeekChar(1);
        if (kind is not ('u' or 'U'))
        {
            return false;
        }

        var digits = kind == 'u' ? 4 : 8;

        // Consume '\' and 'u'/'U'
        AdvanceChar(2);

        var codePoint = 0;
        for (var i = 0; i < digits; i++)
        {
            var ch = PeekChar();
            if (!SyntaxFacts.IsHexDigit(ch))
            {
                Reset(start);
                return false;
            }

            codePoint = (codePoint << 4) + SyntaxFacts.HexValue(ch);
            AdvanceChar();
        }

        if (kind == 'u')
        {
            decoded = (char)codePoint;
            return true;
        }

        // \UXXXXXXXX
        if ((uint)codePoint > 0x10FFFF)
        {
            Reset(start);
            return false;
        }

        if (codePoint <= 0xFFFF)
        {
            decoded = (char)codePoint;
            return true;
        }

        // Convert to surrogate pair
        codePoint -= 0x10000;
        decoded = (char)(0xD800 + (codePoint >> 10));
        surrogate = (char)(0xDC00 + (codePoint & 0x3FF));
        return true;
    }

    #endregion
}
