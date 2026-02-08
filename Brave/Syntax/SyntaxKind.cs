using System;
using System.Collections.Generic;
using System.Text;

namespace Brave.Syntax;

public enum SyntaxKind
{
    None,
    IdentifierToken,
    StringLiteralToken,
    NumericLiteralToken,


    FirstWellKnownText = DollarToken,

    DollarToken = 101,                   // $
    PlusToken = 102,                     // +
    MinusToken = 103,                    // -
    AsteriskToken = 104,                 // *
    SlashToken = 105,                    // /
    OpenParenToken = 106,                // (
    CloseParenToken = 107,               // )
    SemicolonToken = 108,                // ;
    EqualsToken = 109,                   // =
    PlusPlusToken = 110,                 // ++
    MinusMinusToken = 111,               // --
    PlusEqualsToken = 112,               // +=
    MinusEqualsToken = 113,              // -=
    AsteriskEqualsToken = 114,           // *=
    SlashEqualsToken = 115,              // /=
    OpenBracketToken = 116,              // [
    CloseBracketToken = 117,             // ]
    OpenBraceToken = 118,                // {
    CloseBraceToken = 119,               // }
    BangToken = 120,                     // !
    CommaToken = 121,                    // ,
    DotToken = 122,                      // .
    EqualEqualToken = 123,               // ==
    ParameterKeyword = 124,              // parameter
    AtToken = 125,                       // @
    AmpersandToken = 126,                // &
    AmpersandEqualsToken = 127,          // &=
    AmpersandAmpersandToken = 128,       // &&
    BarToken = 129,                      // |
    BarEqualsToken = 130,                // |=
    BarBarToken = 131,                   // ||
    LessToken = 132,                     // <
    LessEqualsToken = 133,               // <=
    LessLessToken = 134,                 // <<
    LessLessEqualsToken = 135,           // <<=
    GreaterToken = 136,                  // >
    GreaterEqualsToken = 137,            // >=
    GreaterGreaterToken = 138,           // >>
    GreaterGreaterEqualsToken = 139,     // >>=
    QuestionToken = 140,                 // ?
    QuestionQuestionToken = 141,         // ?=
    QuestionQuestionEqualsToken = 142,   // ??=
    ColonToken = 143,                    // :
    CaretToken = 144,                    // ^
    CaretEqualsToken = 145,              // ^=
    BangEqualsToken = 146,               // !=
    TildeToken = 147,                    // ~
    NullKeyword = 148,                   // null

    LastWellKnownText = NullKeyword,
}
