using Brave.Pools;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace Brave.Syntax;

internal sealed partial class Lexer : IDisposable
{
    public const int MaxExpressionLength = 1024;

    private static readonly ObjectPool<char[]> s_identifierPool = new(() => new char[64]);

    private readonly char[] _buffer;
    private readonly int _length;

    private char[] _identifierBuffer;
    private int _identifierLength;

    private readonly StringTable _stringTable = StringTable.GetInstance();
    private readonly PooledStringBuilder _stringBuilder = PooledStringBuilder.GetInstance();

    private int _position = 0;

    public Lexer(string input)
    {
        if (input.Length > MaxExpressionLength)
        {
            throw new ArgumentException($"Input length exceeds maximum allowed length of {MaxExpressionLength}.", nameof(input));
        }

        _buffer = ArrayPool<char>.Shared.Rent(input.Length);
        input.AsSpan().CopyTo(_buffer);

        _length = input.Length;

        _identifierBuffer = s_identifierPool.Allocate();
        _identifierLength = 0;
    }

    public IEnumerable<SyntaxToken> LexToEnd()
    {
        while (!IsAtEnd())
        {
            SkipWhitespace();
            if (IsAtEnd())
            {
                break;
            }

            var token = NextToken();

            if (token != null)
            {
                yield return token;
            }
        }
    }

    public ImmutableArray<SyntaxToken> LexToEndArray()
    {
        using var builder = ImmutableArrayBuilder<SyntaxToken>.Rent();
        while (!IsAtEnd())
        {
            SkipWhitespace();
            if (IsAtEnd())
            {
                break;
            }

            var token = NextToken();

            if (token != null)
            {
                builder.Add(token);
            }
        }

        return builder.ToImmutable();
    }

    private bool IsAtEnd()
        => _position >= _length;

    private void AdvanceChar()
        => AdvanceChar(1);

    private void AdvanceChar(int n)
    {
        _position += n;
        Debug.Assert(_position >= 0, "Position in text cannot be negative.");
    }

    private bool TryAdvance(char c)
    {
        if (PeekChar() != c)
        {
            return false;
        }

        AdvanceChar();
        return true;
    }

    private void AdvancePastNewLine()
    {
        AdvanceChar(GetNewLineWidth());
    }

    private void Reset(int position)
    {
        Debug.Assert(_position >= 0, "Position in text cannot be negative.");

        _position = position;
    }

    private int GetNewLineWidth()
    {
        Debug.Assert(SyntaxFacts.IsNewLine(this.PeekChar()));
        return GetNewLineWidth(this.PeekChar(), this.PeekChar(1));
    }

    private static int GetNewLineWidth(char currentChar, char nextChar)
    {
        Debug.Assert(SyntaxFacts.IsNewLine(currentChar));
        return currentChar == '\r' && nextChar == '\n' ? 2 : 1;
    }


    private char PeekChar() => _position < _length ? _buffer[_position] : '\0';

    private char PeekChar(int delta)
    {
        var position = _position;
        AdvanceChar(delta);
        var ch = PeekChar();
        Reset(position);
        return ch;
    }

    private char PreviousChar()
        => PeekChar(-1);

    private void SkipWhitespace(bool includeNewLines = true)
    {
        while (true)
        {
            var currentChar = PeekChar();

            if (currentChar == '\0')
            {
                return;
            }

            if (SyntaxFacts.IsWhitespace(currentChar))
            {
                AdvanceChar();
                continue;
            }

            if (includeNewLines && SyntaxFacts.IsNewLine(currentChar))
            {
                AdvancePastNewLine(); // Handles "\r\n" as a single newline.
                continue;
            }

            return;
        }
    }

    private string Intern(string value)
    {
        return _stringTable.Add(value);
    }

    private string Intern(ReadOnlySpan<char> value)
    {
        return _stringTable.Add(value);
    }

    private string Intern(char[] array, int start, int length)
        => _stringTable.Add(array, start, length);

    private string Intern(int start, int length)
    {
        return _stringTable.Add(_buffer, start, length);
    }

    public void Dispose()
    {
        ArrayPool<char>.Shared.Return(_buffer);
        _stringTable.Free();
        _stringBuilder.Free();

        if (_identifierBuffer.Length == 64)
        {
            s_identifierPool.Free(_identifierBuffer);
        }
        _identifierBuffer = null!;

        GC.SuppressFinalize(this);
    }

    private void FreeIdentifierBuffer()
    {
        if (_identifierBuffer.Length == 64)
        {
            s_identifierPool.Free(_identifierBuffer);
        }
        _identifierBuffer = null!;
    }


    ~Lexer()
    {
        Dispose();
    }
}
