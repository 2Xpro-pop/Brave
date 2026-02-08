using Brave.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Brave.Syntax;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed class SyntaxToken
{
    private const int IdentifierCacheSize = 1 << 8;
    private const int IdentifierCacheMask = IdentifierCacheSize - 1;

    private const int TokenWithValueCacheSize = 1 << 10;
    private const int TokenWithValueCacheMask = TokenWithValueCacheSize - 1;

    private static readonly ArrayElement<SyntaxToken?>[] s_identifierCache = new ArrayElement<SyntaxToken?>[IdentifierCacheSize];
    private static readonly ArrayElement<SyntaxToken>[] s_cachedTokens;
    private static readonly ArrayElement<SyntaxToken?>[] s_cachedTokenWithValue = new ArrayElement<SyntaxToken?>[TokenWithValueCacheSize];

    static SyntaxToken()
    {

        var wellKnownKindsLength = SyntaxKind.LastWellKnownText - SyntaxKind.FirstWellKnownText + 1;

        s_cachedTokens = new ArrayElement<SyntaxToken>[wellKnownKindsLength];

        for (int i = 0; i < wellKnownKindsLength; i++)
        {
            var kind = (SyntaxKind.FirstWellKnownText + i);
            s_cachedTokens[i].Value = new SyntaxToken()
            {
                Kind = kind,
                Text = GetTextForKind(kind),
            };
        }
    }

    private static string GetTextForKind(SyntaxKind kind)
    {
        return kind switch
        {
            SyntaxKind.DollarToken => "$",
            SyntaxKind.PlusToken => "+",
            SyntaxKind.MinusToken => "-",
            SyntaxKind.AsteriskToken => "*",
            SyntaxKind.SlashToken => "/",
            SyntaxKind.OpenParenToken => "(",
            SyntaxKind.CloseParenToken => ")",
            SyntaxKind.SemicolonToken => ";",
            SyntaxKind.EqualsToken => "=",
            SyntaxKind.PlusPlusToken => "++",
            SyntaxKind.MinusMinusToken => "--",
            SyntaxKind.PlusEqualsToken => "+=",
            SyntaxKind.MinusEqualsToken => "-=",
            SyntaxKind.AsteriskEqualsToken => "*=",
            SyntaxKind.SlashEqualsToken => "/=",
            SyntaxKind.OpenBracketToken => "[",
            SyntaxKind.CloseBracketToken => "]",
            SyntaxKind.OpenBraceToken => "{",
            SyntaxKind.CloseBraceToken => "}",
            SyntaxKind.BangToken => "!",
            SyntaxKind.CommaToken => ",",
            SyntaxKind.DotToken => ".",
            SyntaxKind.EqualEqualToken => "==",
            SyntaxKind.ParameterKeyword => "parameter",
            SyntaxKind.AtToken => "@",
            SyntaxKind.AmpersandToken => "&",
            SyntaxKind.AmpersandEqualsToken => "&=",
            SyntaxKind.AmpersandAmpersandToken => "&&",
            SyntaxKind.BarToken => "|",
            SyntaxKind.BarEqualsToken => "|=",
            SyntaxKind.BarBarToken => "||",
            SyntaxKind.LessToken => "<",
            SyntaxKind.LessEqualsToken => "<=",
            SyntaxKind.LessLessToken => "<<",
            SyntaxKind.LessLessEqualsToken => "<<=",
            SyntaxKind.GreaterToken => ">",
            SyntaxKind.GreaterEqualsToken => ">=",
            SyntaxKind.GreaterGreaterToken => ">>",
            SyntaxKind.GreaterGreaterEqualsToken => ">>=",
            SyntaxKind.QuestionToken => "?",
            SyntaxKind.QuestionQuestionToken => "??",
            SyntaxKind.QuestionQuestionEqualsToken => "??=",
            SyntaxKind.ColonToken => ":",
            SyntaxKind.CaretToken => "^",
            SyntaxKind.CaretEqualsToken => "^=",
            SyntaxKind.BangEqualsToken => "!=",
            SyntaxKind.TildeToken => "~",
            SyntaxKind.NullKeyword => "null",

            _ => throw new ArgumentOutOfRangeException(nameof(kind), $"No text for syntax kind {kind}"),
        };
    }


    private SyntaxToken()
    {
        Text = null!;
    }

    public bool IsIdentifier => Kind == SyntaxKind.IdentifierToken;

    public string Text
    {
        get; private init;
    }

    public object Value
    {
        get => field ?? Text;
        private init;
    }

    public SyntaxKind Kind
    {
        get; private init;
    }

    internal static SyntaxToken CreateToken(SyntaxKind syntaxKind)
    {
        if (syntaxKind >= SyntaxKind.FirstWellKnownText && syntaxKind <= SyntaxKind.LastWellKnownText)
        {
            var index = syntaxKind - SyntaxKind.FirstWellKnownText;
            return s_cachedTokens[index];
        }

        throw new ArgumentOutOfRangeException(nameof(syntaxKind), $"No cached token for syntax kind {syntaxKind}");
    }

    internal static SyntaxToken CreateIdentifier(string text)
    {
        var index = text.GetHashCode() & IdentifierCacheMask;
        var cached = s_identifierCache[index].Value;

        if (cached != null && cached.Text == text)
        {
            return cached;
        }

        var newToken = new SyntaxToken()
        {
            Kind = SyntaxKind.IdentifierToken,
            Text = text,
        };

        s_identifierCache[index].Value = newToken;

        return newToken;
    }

    internal static SyntaxToken CreateLiteral(SyntaxKind kind, string text, object value)
    {
        var index = text.GetHashCode() ^ value.GetHashCode() ^ kind.GetHashCode();
        index &= TokenWithValueCacheMask;

        var cached = s_cachedTokenWithValue[index].Value;

        if (cached != null && cached.Kind == kind && cached.Text == text && Equals(cached.Value, value))
        {
            return cached;
        }

        var token = new SyntaxToken()
        {
            Kind = kind,
            Text = text,
            Value = value,
        };

        s_cachedTokenWithValue[index].Value = token;

        return token;
    }

    internal bool IsCached
    {
        get
        {
            // Well-known tokens are permanently cached
            if (Kind >= SyntaxKind.FirstWellKnownText && Kind < SyntaxKind.LastWellKnownText)
            {
                var index = (int)Kind - (int)SyntaxKind.FirstWellKnownText;
                return ReferenceEquals(this, s_cachedTokens[index].Value);
            }

            // Identifier tokens are cached by text hash slot
            if (Kind == SyntaxKind.IdentifierToken)
            {
                var index = Text.GetHashCode() & IdentifierCacheMask;
                return ReferenceEquals(this, s_identifierCache[index].Value);
            }

            // Tokens with values are cached by combined hash slot
            var computedIndex = (Text.GetHashCode() ^ Value.GetHashCode() ^ Kind.GetHashCode()) & TokenWithValueCacheMask;
            return ReferenceEquals(this, s_cachedTokenWithValue[computedIndex].Value);
        }
    }

    private string GetDebuggerDisplay()
    {
        return $"{Kind}, \"{Text}\", Cached={IsCached}";
    }
}
