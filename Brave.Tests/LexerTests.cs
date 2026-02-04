using Brave.Syntax;
using System.Diagnostics.Metrics;

namespace Brave.Tests;

public class LexerTests
{
    private static List<SyntaxToken> LexAll(string text)
    {
        using var lexer = new Lexer(text);
        var tokens = new List<SyntaxToken>();

        var token = lexer.NextToken();

        while (token != null)
        {
            tokens.Add(token);
            token = lexer.NextToken();
        }

        return tokens;
    }

    private static void AssertToken(SyntaxToken token, SyntaxKind kind, string text)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(token.Kind, Is.EqualTo(kind));
            Assert.That(token.Text, Is.EqualTo(text));
        }
    }

    private static void AssertLiteral<T>(SyntaxToken token, string text, T expected)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(token.Kind, Is.EqualTo(SyntaxKind.NumericLiteralToken));
            Assert.That(token.Text, Is.EqualTo(text));
            Assert.That(token.Value, Is.TypeOf<T>());
            Assert.That((T)token.Value, Is.EqualTo(expected));
        }
    }


    [Test]
    public void Whitespace_Skipped()
    {
        var tokens = LexAll("   \r\n\t  + ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertToken(tokens[0], SyntaxKind.PlusToken, "+");
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Identifier_Lexed()
    {
        var tokens = LexAll("$hello");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertToken(tokens[0], SyntaxKind.DollarToken, "$");
        AssertToken(tokens[1], SyntaxKind.IdentifierToken, "hello");
    }

    [Test]
    public void Expression_Lexed()
    {
        var tokens = LexAll("$Hello + 11 == 25");

        Assert.That(tokens, Has.Count.EqualTo(6));
        AssertToken(tokens[0], SyntaxKind.DollarToken, "$");
        AssertToken(tokens[1], SyntaxKind.IdentifierToken, "Hello");
        AssertToken(tokens[2], SyntaxKind.PlusToken, "+");
        AssertToken(tokens[3], SyntaxKind.NumericLiteralToken, "11");
        AssertToken(tokens[4], SyntaxKind.EqualEqualToken, "==");
        AssertToken(tokens[5], SyntaxKind.NumericLiteralToken, "25");
    }

    [Test]
    public void Cache_Test()
    {
        // chance to fail is test is ~ 1.16%
        // because we have 3 identificator which take cache with size is 256

        var tokens = LexAll("$Hello + $Hello + $Hello - $A * $B == $A - $B + 50 - 50");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tokens, Has.Count.EqualTo(24));

            // Check is all "Hello" reference equals
            Assert.That(ReferenceEquals(tokens[1], tokens[4]));
            Assert.That(ReferenceEquals(tokens[4], tokens[7]));

            // Check is all "A" reference equals
            Assert.That(ReferenceEquals(tokens[10], tokens[16]));

            // Check is all "B" reference eqauls
            Assert.That(ReferenceEquals(tokens[13], tokens[19]));

            // Check is all "50" reference equals
            Assert.That(ReferenceEquals(tokens[21], tokens[23]));

            // Chech is all IsCached
            Assert.That(tokens.All(x => x.IsCached));
        }
    }

    [Test]
    public void Operators_And_Punctuators_Lexed()
    {
        var tokens = LexAll("++ += + -- -= - *= * /= / ( ) [ ] { } = == $ ! ; , .");

        Assert.Multiple(() =>
        {
            Assert.That(tokens.Select(t => t.Kind), Is.EquivalentTo(
            [
                SyntaxKind.PlusPlusToken,
                SyntaxKind.PlusEqualsToken,
                SyntaxKind.PlusToken,

                SyntaxKind.MinusMinusToken,
                SyntaxKind.MinusEqualsToken,
                SyntaxKind.MinusToken,

                SyntaxKind.AsteriskEqualsToken,
                SyntaxKind.AsteriskToken,

                SyntaxKind.SlashEqualsToken,
                SyntaxKind.SlashToken,

                SyntaxKind.OpenParenToken,
                SyntaxKind.CloseParenToken,

                SyntaxKind.OpenBracketToken,
                SyntaxKind.CloseBracketToken,

                SyntaxKind.OpenBraceToken,
                SyntaxKind.CloseBraceToken,

                SyntaxKind.EqualsToken,
                SyntaxKind.EqualEqualToken,

                SyntaxKind.DollarToken,
                SyntaxKind.BangToken,

                SyntaxKind.SemicolonToken,
                SyntaxKind.CommaToken,
                SyntaxKind.DotToken,
            ]));

            // Quick sanity for caching of well-known tokens
            Assert.That(tokens.All(t => t.IsCached), Is.True);
        });
    }

    [Test]
    public void Mixed_Expression_Lexed_WithCorrectKinds()
    {
        var tokens = LexAll("$A += 10; $A == 10;");

        // $ A += 10 ; $ A == 10 ;
        Assert.That(tokens.Select(t => t.Kind), Is.EquivalentTo(
        [
            SyntaxKind.DollarToken,
            SyntaxKind.IdentifierToken,
            SyntaxKind.PlusEqualsToken,
            SyntaxKind.NumericLiteralToken,
            SyntaxKind.SemicolonToken,

            SyntaxKind.DollarToken,
            SyntaxKind.IdentifierToken,
            SyntaxKind.EqualEqualToken,
            SyntaxKind.NumericLiteralToken,
            SyntaxKind.SemicolonToken,
        ]));
    }

    [Test]
    public void Numeric_Literal_Cache_Works_ForSameTextAndValue()
    {
        var tokens = LexAll("50 + 50 + 50 ;");

        // 50, +, 50, +, 50, ;
        Assert.That(tokens, Has.Count.EqualTo(6));

        using (Assert.EnterMultipleScope())
        {
            AssertToken(tokens[0], SyntaxKind.NumericLiteralToken, "50");
            AssertToken(tokens[2], SyntaxKind.NumericLiteralToken, "50");
            AssertToken(tokens[4], SyntaxKind.NumericLiteralToken, "50");

            Assert.That(ReferenceEquals(tokens[0], tokens[2]), Is.True);
            Assert.That(ReferenceEquals(tokens[2], tokens[4]), Is.True);

            Assert.That(tokens.All(t => t.IsCached), Is.True);
        }
    }

    [Test]
    public void Keyword_Parameter_Lexed()
    {
        var tokens = LexAll("parameter ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertToken(tokens[0], SyntaxKind.ParameterKeyword, "parameter");
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Keyword_Parameter_DoesNotMatch_WhenContinued()
    {
        var tokens = LexAll("parameterX ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertToken(tokens[0], SyntaxKind.IdentifierToken, "parameterX");
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Identifier_Ascii_WithUnderscore_AndDigits()
    {
        var tokens = LexAll("a _a a1 _a1 ;");

        Assert.That(tokens, Has.Count.EqualTo(5));
        AssertToken(tokens[0], SyntaxKind.IdentifierToken, "a");
        AssertToken(tokens[1], SyntaxKind.IdentifierToken, "_a");
        AssertToken(tokens[2], SyntaxKind.IdentifierToken, "a1");
        AssertToken(tokens[3], SyntaxKind.IdentifierToken, "_a1");
        AssertToken(tokens[4], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Identifier_Verbatim_AtPrefix_ReturnsWithoutAt()
    {
        // По твоей реализации ParseIdentifierSlow: '@' потребляется и не входит в Text
        var tokens = LexAll("@hello ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertToken(tokens[0], SyntaxKind.IdentifierToken, "hello");
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Identifier_Unicode_Lexed()
    {
        var tokens = LexAll("счётчик ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertToken(tokens[0], SyntaxKind.IdentifierToken, "счётчик");
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Identifier_UnicodeEscape_uXXXX_Lexed()
    {
        // \u0061 == 'a'
        var tokens = LexAll("\\u0061BC ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertToken(tokens[0], SyntaxKind.IdentifierToken, "aBC");
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Identifier_UnicodeEscape_UXXXXXXXX_SurrogatePair_Lexed()
    {
        // U+1F600 = 😀
        var tokens = LexAll("\\U0001F600face ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertToken(tokens[0], SyntaxKind.IdentifierToken, "😀face");
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Numeric_Integer_Decimal_Lexed()
    {
        var tokens = LexAll("123 ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertLiteral(tokens[0], "123", 123);
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Numeric_Integer_Decimal_WithUnderscores_Lexed()
    {
        var tokens = LexAll("1_000_000 ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertLiteral(tokens[0], "1_000_000", 1_000_000);
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Numeric_Integer_Hex_Lexed()
    {
        var tokens = LexAll("0xFF ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertLiteral(tokens[0], "0xFF", 255);
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Numeric_Integer_Binary_Lexed()
    {
        var tokens = LexAll("0b1010 ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertLiteral(tokens[0], "0b1010", 10);
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Numeric_FloatSuffix_f_Lexed()
    {
        var tokens = LexAll("1.5f ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertLiteral(tokens[0], "1.5f", 1.5f);
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Numeric_Double_Default_WhenDecimalPoint()
    {
        var tokens = LexAll("2.25 ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertLiteral(tokens[0], "2.25", 2.25d);
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Numeric_DoubleSuffix_d_Lexed()
    {
        var tokens = LexAll("2d ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertLiteral(tokens[0], "2d", 2d);
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Numeric_Integer_Suffixes_U_L_UL()
    {
        var tokens = LexAll("1u 2L 3ul ;");

        Assert.That(tokens, Has.Count.EqualTo(4));
        AssertLiteral(tokens[0], "1u", (uint)1);
        AssertLiteral(tokens[1], "2L", (long)2);
        AssertLiteral(tokens[2], "3ul", (ulong)3);
        AssertToken(tokens[3], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Numeric_Exponent_Double_Lexed()
    {
        var tokens = LexAll("1e-3 ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertLiteral(tokens[0], "1e-3", 0.001d);
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }

    [Test]
    public void Numeric_DecimalSuffix_m_Lexed()
    {
        var tokens = LexAll("10.0m ;");

        Assert.That(tokens, Has.Count.EqualTo(2));
        AssertLiteral(tokens[0], "10.0m", 10.0m);
        AssertToken(tokens[1], SyntaxKind.SemicolonToken, ";");
    }
}
