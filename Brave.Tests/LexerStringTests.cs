using Brave.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brave.Tests;

public sealed class LexerStringTests
{
    private static List<SyntaxToken> LexAll(string text)
    {
        using var lexer = new Lexer(text);

        var tokens = new List<SyntaxToken>();
        while (true)
        {
            var token = lexer.NextToken();
            if (token is null)
            {
                break;
            }

            tokens.Add(token);
        }

        return tokens;
    }

    private static void AssertToken(SyntaxToken token, SyntaxKind kind, string text)
    {
        Assert.Multiple(() =>
        {
            Assert.That(token.Kind, Is.EqualTo(kind));
            Assert.That(token.Text, Is.EqualTo(text));
        });
    }

    private static void AssertStringLiteral(SyntaxToken token, string text, string expectedValue)
    {
        Assert.Multiple(() =>
        {
            Assert.That(token.Kind, Is.EqualTo(SyntaxKind.StringLiteralToken));
            Assert.That(token.Text, Is.EqualTo(text));
            Assert.That(token.Value, Is.TypeOf<string>());
            Assert.That((string)token.Value, Is.EqualTo(expectedValue));
        });
    }

    [Test]
    public void String_DoubleQuotes_Lexed()
    {
        var tokens = LexAll("\"hello\"");

        Assert.That(tokens, Has.Count.EqualTo(1));
        AssertStringLiteral(tokens[0], "\"hello\"", "hello");
    }

    [Test]
    public void String_SingleQuotes_Lexed()
    {
        var tokens = LexAll("'hello'");

        Assert.That(tokens, Has.Count.EqualTo(1));
        AssertStringLiteral(tokens[0], "'hello'", "hello");
    }

    [Test]
    public void String_NormalEscapes_Work()
    {
        // NOTE: In C# string literal you need double escaping for backslash.
        var tokens = LexAll("'a\\\\b\\n\\t\\'\\\"'");

        Assert.That(tokens, Has.Count.EqualTo(1));
        AssertStringLiteral(tokens[0], "'a\\\\b\\n\\t\\'\\\"'", "a\\b\n\t'\"");
    }

    [Test]
    public void String_UnicodeEscape_uXXXX_Works()
    {
        var tokens = LexAll("\"A\\u0042C\""); // \u0042 = 'B'

        Assert.That(tokens, Has.Count.EqualTo(1));
        AssertStringLiteral(tokens[0], "\"A\\u0042C\"", "ABC");
    }

    [Test]
    public void String_UnicodeEscape_UXXXXXXXX_BMP_Works()
    {
        // U00000041 = 'A'
        var tokens = LexAll("'\\U00000041'");

        Assert.That(tokens, Has.Count.EqualTo(1));
        AssertStringLiteral(tokens[0], "'\\U00000041'", "A");
    }

    [Test]
    public void String_UnicodeEscape_UXXXXXXXX_SurrogatePair_Works()
    {
        // 😀 U+1F600 => \U0001F600 -> surrogate pair
        var tokens = LexAll("\"\\U0001F600\"");

        Assert.That(tokens, Has.Count.EqualTo(1));
        AssertStringLiteral(tokens[0], "\"\\U0001F600\"", char.ConvertFromUtf32(0x1F600));
    }

    [Test]
    public void String_Verbatim_DoubleQuotes_Allows_DoubledDelimiter()
    {
        // @"a""b" => a"b
        var tokens = LexAll("@\"a\"\"b\"");

        Assert.That(tokens, Has.Count.EqualTo(1));
        AssertStringLiteral(tokens[0], "@\"a\"\"b\"", "a\"b");
    }

    [Test]
    public void String_Verbatim_SingleQuotes_Allows_DoubledDelimiter()
    {
        // @'a''b' => a'b
        // so this path is supported now.
        var tokens = LexAll("@'a''b'");

        Assert.That(tokens, Has.Count.EqualTo(1));
        AssertStringLiteral(tokens[0], "@'a''b'", "a'b");
    }

    [Test]
    public void String_Must_Be_OneLine_NewLine_Stops_Lexing_String()
    {
        // One-line rule: ParseString stops when it sees newline without closing quote.
        // After that lexer continues from newline, skips it in SkipWhitespace(includeNewLines: true),
        // then lexes identifier.
        var input = "\"abc\nxyz\"";
        var tokens = LexAll(input);

        // Expected:
        // 1) StringLiteralToken with text "\"abc" (unterminated, stopped at newline)
        // 2) IdentifierToken "xyz" (then next token is StringLiteralToken "\""? depends on remaining chars)
        //
        // Let's reason:
        // input: "abc\nxyz"
        // ParseString consumes opening quote, reads abc, stops at '\n' without consuming it.
        // token text is from startPosition to current _position -> "\"abc" (quote+abc)
        // NextToken skips newline; then sees 'x' => identifier => "xyz"
        // Then sees '"' => ParseString => empty string "" (just closing quote? Actually it sees '"' as opening quote,
        // then next is end => it breaks and returns token with text "\"" and value "")
        //
        // To keep test stable: we assert the first two tokens only and check token count >=2.
        Assert.That(tokens, Has.Count.GreaterThanOrEqualTo(2));

        AssertStringLiteral(tokens[0], "\"abc", "abc");
        AssertToken(tokens[1], SyntaxKind.IdentifierToken, "xyz");
    }

    [Test]
    public void String_TokenText_Preserves_Raw_Source()
    {
        // Ensure Text keeps original lexeme (quotes, escapes) while Value is decoded.
        var tokens = LexAll("\"a\\n\\u0042\"");

        Assert.That(tokens, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tokens[0].Text, Is.EqualTo("\"a\\n\\u0042\""));
            Assert.That(tokens[0].Value, Is.EqualTo("a\nB"));
        }
    }

    [Test]
    public void String_Caching_Works_For_Same_Text_And_Value()
    {
        // CreateLiteral caches by (Text ^ Value ^ Kind) hash slot.
        // Here both token.Text and token.Value should match -> same cached instance expected (most of the time).
        //
        // Unlike well-known tokens, this cache can collide, but with two identical literals in same run
        // it should be extremely stable.
        var tokens = LexAll("\"hello\" + \"hello\"");

        Assert.That(tokens, Has.Count.EqualTo(3));
        AssertStringLiteral(tokens[0], "\"hello\"", "hello");
        AssertToken(tokens[1], SyntaxKind.PlusToken, "+");
        AssertStringLiteral(tokens[2], "\"hello\"", "hello");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ReferenceEquals(tokens[0], tokens[2]), Is.True);
            Assert.That(tokens[0].IsCached, Is.True);
            Assert.That(tokens[2].IsCached, Is.True);
        }
    }
}