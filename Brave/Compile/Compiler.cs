using Brave.Commands;
using Brave.Pools;
using Brave.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Brave.Compile;

public static class Compiler
{
    public static ImmutableArray<CommandInstruction> Compile(string expression, bool useDirectSetResource = false)
    {
        if(CompilerCache.TryGet(expression, useDirectSetResource, out var cached))
        {
            return cached;
        }

        using var lexer = new Lexer(expression);
        var tokens = lexer.LexToEndArray();

        var instructions = Compile(tokens, useDirectSetResource);

        CompilerCache.Add(expression, useDirectSetResource, instructions);

        return instructions;
    }

    public static ImmutableArray<CommandInstruction> Compile(ImmutableArray<SyntaxToken> syntaxTokens, bool useDirectSetResource = false)
    {
        using var builder = ImmutableArrayBuilder<CommandInstruction>.Rent();

        var reader = new TokenReader(syntaxTokens);

        while (!reader.IsAtEnd)
        {
            ParseExpression(ref reader, builder, useDirectSetResource);

            if (reader.TryConsume(SyntaxKind.SemicolonToken))
            {
                continue;
            }

            if (!reader.IsAtEnd)
            {
                throw new InvalidOperationException($"Unexpected token '{reader.Current?.Kind ?? SyntaxKind.None}' at '{reader.Current?.Text ?? "null"}'.");
            }
        }

        return builder.ToImmutable();
    }

    private static void ParseExpression(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseAssignment(ref reader, builder, useDirectSetResource);
    }

    private static void ParseAssignment(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        if (TryPeekResourceAssignment(ref reader, out var resourceKey, out var assignmentOperatorKind))
        {
            // Consume "$" + name
            reader.Consume(SyntaxKind.DollarToken);
            var nameToken = reader.ConsumeAny(SyntaxKind.IdentifierToken, SyntaxKind.ParameterKeyword);

            if (nameToken.Kind == SyntaxKind.ParameterKeyword)
            {
                throw new InvalidOperationException("Cannot assign to $parameter.");
            }

            // key includes '$' prefix to match DynamicResource $Key usage
            resourceKey = "$" + nameToken.Text;

            // Consume assignment operator
            reader.Consume(assignmentOperatorKind);

            // Special: ??= (only assign when current is null)
            if (assignmentOperatorKind == SyntaxKind.QuestionQuestionEqualsToken)
            {
                // push current
                builder.Add(new CommandInstruction(CommandOpCode.GetResource, [resourceKey]));

                // if null -> jump to assign (JumpIfNull pops null)
                var jumpAssignIndex = EmitJump(builder, CommandOpCode.JumpIfNull);

                // not null -> skip assign
                var jumpEndIndex = EmitJump(builder, CommandOpCode.Jump);

                var assignIndex = builder.Count;

                ParseAssignment(ref reader, builder, useDirectSetResource); // RHS on stack
                EmitSetResource(builder, useDirectSetResource, resourceKey, RuntimeStack.Indexes.Last);

                // result (C#-like): value after assignment
                builder.Add(new CommandInstruction(CommandOpCode.GetResource, [resourceKey]));

                var endIndex = builder.Count;

                PatchJump(builder, jumpAssignIndex, assignIndex);
                PatchJump(builder, jumpEndIndex, endIndex);

                return;
            }

            // Compound assignment: += -= *= /=
            if (assignmentOperatorKind is SyntaxKind.PlusEqualsToken or SyntaxKind.MinusEqualsToken or SyntaxKind.AsteriskEqualsToken or SyntaxKind.SlashEqualsToken)
            {
                // left (resource value)
                builder.Add(new CommandInstruction(CommandOpCode.GetResource, [resourceKey]));

                // right
                ParseAssignment(ref reader, builder, useDirectSetResource);

                // op
                builder.Add(new CommandInstruction(MapCompoundOperatorToOpCode(assignmentOperatorKind)));

                // store
                EmitSetResource(builder, useDirectSetResource, resourceKey, RuntimeStack.Indexes.Last);

                // result (C#-like): assigned value
                builder.Add(new CommandInstruction(CommandOpCode.GetResource, [resourceKey]));
                return;
            }

            // Simple '='
            ParseAssignment(ref reader, builder, useDirectSetResource); // RHS on stack

            EmitSetResource(builder, useDirectSetResource, resourceKey, RuntimeStack.Indexes.Last);

            // result (C#-like): assigned value
            builder.Add(new CommandInstruction(CommandOpCode.GetResource, [resourceKey]));
            return;
        }

        ParseConditional(ref reader, builder, useDirectSetResource);
    }

    private static void ParseConditional(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseCoalesce(ref reader, builder, useDirectSetResource);

        if (!reader.TryConsume(SyntaxKind.QuestionToken))
        {
            return;
        }

        // condition is on stack
        var jumpElseIndex = EmitJump(builder, CommandOpCode.JumpIfFalse);

        // then
        ParseExpression(ref reader, builder, useDirectSetResource);

        var jumpEndIndex = EmitJump(builder, CommandOpCode.Jump);

        reader.Consume(SyntaxKind.ColonToken);

        var elseIndex = builder.Count;

        // else
        ParseExpression(ref reader, builder, useDirectSetResource);

        var endIndex = builder.Count;

        PatchJump(builder, jumpElseIndex, elseIndex);
        PatchJump(builder, jumpEndIndex, endIndex);
    }

    private static void ParseCoalesce(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseLogicalOrShortCircuit(ref reader, builder, useDirectSetResource);

        if (!reader.TryConsume(SyntaxKind.QuestionQuestionToken))
        {
            return;
        }

        // left is on stack
        var jumpFallbackIndex = EmitJump(builder, CommandOpCode.JumpIfNull);
        var jumpEndIndex = EmitJump(builder, CommandOpCode.Jump);

        var fallbackIndex = builder.Count;

        // right-associative: a ?? (b ?? c)
        ParseCoalesce(ref reader, builder, useDirectSetResource);

        var endIndex = builder.Count;

        PatchJump(builder, jumpFallbackIndex, fallbackIndex);
        PatchJump(builder, jumpEndIndex, endIndex);
    }

    private static void ParseLogicalOrShortCircuit(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseLogicalAndShortCircuit(ref reader, builder, useDirectSetResource);

        if (!reader.IsCurrent(SyntaxKind.BarBarToken))
        {
            return;
        }

        var jumpIfTrueIndexes = new List<int>(capacity: 4);

        while (reader.TryConsume(SyntaxKind.BarBarToken))
        {
            // if left is true => short-circuit to TRUE (JumpIfTrue pops)
            jumpIfTrueIndexes.Add(EmitJump(builder, CommandOpCode.JumpIfTrue));

            ParseLogicalAndShortCircuit(ref reader, builder, useDirectSetResource);
        }

        // final operand check
        jumpIfTrueIndexes.Add(EmitJump(builder, CommandOpCode.JumpIfTrue));

        // all false
        builder.Add(new CommandInstruction(CommandOpCode.Push, [false]));
        var jumpEndIndex = EmitJump(builder, CommandOpCode.Jump);

        var trueIndex = builder.Count;
        builder.Add(new CommandInstruction(CommandOpCode.Push, [true]));

        var endIndex = builder.Count;

        for (var index = 0; index < jumpIfTrueIndexes.Count; index++)
        {
            PatchJump(builder, jumpIfTrueIndexes[index], trueIndex);
        }

        PatchJump(builder, jumpEndIndex, endIndex);
    }

    private static void ParseLogicalAndShortCircuit(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseBitwiseOr(ref reader, builder, useDirectSetResource);

        if (!reader.IsCurrent(SyntaxKind.AmpersandAmpersandToken))
        {
            return;
        }

        var jumpIfFalseIndexes = new List<int>(capacity: 4);

        while (reader.TryConsume(SyntaxKind.AmpersandAmpersandToken))
        {
            // if left is false => short-circuit to FALSE (JumpIfFalse pops)
            jumpIfFalseIndexes.Add(EmitJump(builder, CommandOpCode.JumpIfFalse));

            ParseBitwiseOr(ref reader, builder, useDirectSetResource);
        }

        // final operand check
        jumpIfFalseIndexes.Add(EmitJump(builder, CommandOpCode.JumpIfFalse));

        // all true
        builder.Add(new CommandInstruction(CommandOpCode.Push, [true]));
        var jumpEndIndex = EmitJump(builder, CommandOpCode.Jump);

        var falseIndex = builder.Count;
        builder.Add(new CommandInstruction(CommandOpCode.Push, [false]));

        var endIndex = builder.Count;

        for (var index = 0; index < jumpIfFalseIndexes.Count; index++)
        {
            PatchJump(builder, jumpIfFalseIndexes[index], falseIndex);
        }

        PatchJump(builder, jumpEndIndex, endIndex);
    }

    private static void ParseBitwiseOr(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseBitwiseXor(ref reader, builder, useDirectSetResource);

        while (reader.TryConsume(SyntaxKind.BarToken))
        {
            ParseBitwiseXor(ref reader, builder, useDirectSetResource);
            builder.Add(new CommandInstruction(CommandOpCode.BitwiseOr));
        }
    }

    private static void ParseBitwiseXor(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseBitwiseAnd(ref reader, builder, useDirectSetResource);

        while (reader.TryConsume(SyntaxKind.CaretToken)) 
        {
            ParseBitwiseAnd(ref reader, builder, useDirectSetResource);
            builder.Add(new CommandInstruction(CommandOpCode.BitwiseXor));
        }
    }

    private static void ParseBitwiseAnd(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseEquality(ref reader, builder, useDirectSetResource);

        while (reader.TryConsume(SyntaxKind.AmpersandToken))
        {
            ParseEquality(ref reader, builder, useDirectSetResource);
            builder.Add(new CommandInstruction(CommandOpCode.BitwiseAnd));
        }
    }

    private static void ParseEquality(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseRelational(ref reader, builder, useDirectSetResource);

        while (true)
        {
            if (reader.TryConsume(SyntaxKind.EqualEqualToken))
            {
                ParseRelational(ref reader, builder, useDirectSetResource);
                builder.Add(new CommandInstruction(CommandOpCode.Equal));
                continue;
            }

            if (reader.TryConsume(SyntaxKind.BangEqualsToken))
            {
                ParseRelational(ref reader, builder, useDirectSetResource);
                builder.Add(new CommandInstruction(CommandOpCode.NotEqual));
                continue;
            }

            break;
        }
    }

    private static void ParseRelational(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseAdditive(ref reader, builder, useDirectSetResource);

        while (true)
        {
            if (reader.TryConsume(SyntaxKind.LessToken))
            {
                ParseAdditive(ref reader, builder, useDirectSetResource);
                builder.Add(new CommandInstruction(CommandOpCode.LessThan));
                continue;
            }

            if (reader.TryConsume(SyntaxKind.LessEqualsToken))
            {
                ParseAdditive(ref reader, builder, useDirectSetResource);
                builder.Add(new CommandInstruction(CommandOpCode.LessOrEqual));
                continue;
            }

            if (reader.TryConsume(SyntaxKind.GreaterToken))
            {
                ParseAdditive(ref reader, builder, useDirectSetResource);
                builder.Add(new CommandInstruction(CommandOpCode.GreaterThan));
                continue;
            }

            if (reader.TryConsume(SyntaxKind.GreaterEqualsToken))
            {
                ParseAdditive(ref reader, builder, useDirectSetResource);
                builder.Add(new CommandInstruction(CommandOpCode.GreaterOrEqual));
                continue;
            }

            // Shifts are in SyntaxKind, but you don't have opcodes yet (<<, >>)
            if (reader.IsCurrent(SyntaxKind.LessLessToken) ||
                reader.IsCurrent(SyntaxKind.GreaterGreaterToken) ||
                reader.IsCurrent(SyntaxKind.LessLessEqualsToken) ||
                reader.IsCurrent(SyntaxKind.GreaterGreaterEqualsToken))
            {
                throw new InvalidOperationException("Shift operators are tokenized but not supported by CommandOpCode yet.");
            }

            break;
        }
    }

    private static void ParseAdditive(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseMultiplicative(ref reader, builder, useDirectSetResource);

        while (true)
        {
            if (reader.TryConsume(SyntaxKind.PlusToken))
            {
                ParseMultiplicative(ref reader, builder, useDirectSetResource);
                builder.Add(new CommandInstruction(CommandOpCode.Add));
                continue;
            }

            if (reader.TryConsume(SyntaxKind.MinusToken))
            {
                ParseMultiplicative(ref reader, builder, useDirectSetResource);
                builder.Add(new CommandInstruction(CommandOpCode.Subtract));
                continue;
            }

            break;
        }
    }

    private static void ParseMultiplicative(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        ParseUnary(ref reader, builder, useDirectSetResource);

        while (true)
        {
            if (reader.TryConsume(SyntaxKind.AsteriskToken))
            {
                ParseUnary(ref reader, builder, useDirectSetResource);
                builder.Add(new CommandInstruction(CommandOpCode.Multiply));
                continue;
            }

            if (reader.TryConsume(SyntaxKind.SlashToken))
            {
                ParseUnary(ref reader, builder, useDirectSetResource);
                builder.Add(new CommandInstruction(CommandOpCode.Divide));
                continue;
            }

            break;
        }
    }

    private static void ParseUnary(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        // ++$X / --$X (resource-only)
        if (reader.TryConsume(SyntaxKind.PlusPlusToken))
        {
            var key = ParseResourceKey(ref reader);

            builder.Add(new CommandInstruction(CommandOpCode.PreIncrementResource, [key, Boxes.BoxedInt0]));
            return;
        }

        if (reader.TryConsume(SyntaxKind.MinusMinusToken))
        {
            var key = ParseResourceKey(ref reader);

            builder.Add(new CommandInstruction(CommandOpCode.PreDecrementResource, [key]));
            return;
        }

        // !expr
        if (reader.TryConsume(SyntaxKind.BangToken))
        {
            ParseUnary(ref reader, builder, useDirectSetResource);
            builder.Add(new CommandInstruction(CommandOpCode.LogicalNot));
            return;
        }

        // -expr
        if (reader.TryConsume(SyntaxKind.MinusToken))
        {
            ParseUnary(ref reader, builder, useDirectSetResource);
            builder.Add(new CommandInstruction(CommandOpCode.Negate));
            return;
        }

        // +expr (no-op)
        if (reader.TryConsume(SyntaxKind.PlusToken))
        {
            ParseUnary(ref reader, builder, useDirectSetResource);
            return;
        }

        // ~expr
        if (reader.TryConsume(SyntaxKind.TildeToken))
        {
            ParseUnary(ref reader, builder, useDirectSetResource);
            builder.Add(new CommandInstruction(CommandOpCode.BitwiseNot));
            return;
        }

        ParsePrimary(ref reader, builder, useDirectSetResource);
    }

    private static void ParsePrimary(ref TokenReader reader, ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource)
    {
        if (reader.TryConsume(SyntaxKind.OpenParenToken))
        {
            ParseExpression(ref reader, builder, useDirectSetResource);
            reader.Consume(SyntaxKind.CloseParenToken);
            return;
        }

        if (reader.IsCurrent(SyntaxKind.StringLiteralToken) || reader.IsCurrent(SyntaxKind.NumericLiteralToken))
        {
            var literal = reader.Consume(reader.Current!.Kind);
            builder.Add(new CommandInstruction(CommandOpCode.Push, [GetLiteralValue(literal)!]));
            return;
        }

        if (reader.IsCurrent(SyntaxKind.NullKeyword))
        {
            reader.Consume(SyntaxKind.NullKeyword);
            builder.Add(new CommandInstruction(CommandOpCode.Push, [null!]));
            return;
        }

        if(reader.IsCurrent(SyntaxKind.TrueKeyword))
        {
            reader.Consume(SyntaxKind.TrueKeyword);
            builder.Add(new CommandInstruction(CommandOpCode.Push, [true]));
            return;
        }

        if(reader.IsCurrent(SyntaxKind.FalseKeyword))
        {
            reader.Consume(SyntaxKind.FalseKeyword);
            builder.Add(new CommandInstruction(CommandOpCode.Push, [false]));
            return;
        }

        if (reader.TryConsume(SyntaxKind.DollarToken))
        {
            var name = reader.ConsumeAny(SyntaxKind.IdentifierToken, SyntaxKind.ParameterKeyword, SyntaxKind.SelfKeyword);

            if (name.Kind == SyntaxKind.ParameterKeyword)
            {
                builder.Add(new CommandInstruction(CommandOpCode.PushParameter));
                return;
            }

            if (name.Kind == SyntaxKind.SelfKeyword)
            {
                builder.Add(new CommandInstruction(CommandOpCode.PushSelf));
                return;
            }

            var key = "$" + name.Text;

            // Post ++ / -- on resource
            if (reader.TryConsume(SyntaxKind.PlusPlusToken))
            {
                builder.Add(new CommandInstruction(CommandOpCode.PostIncrementResource, [key]));
                return;
            }

            if (reader.TryConsume(SyntaxKind.MinusMinusToken))
            {
                builder.Add(new CommandInstruction(CommandOpCode.PostDecrementResource, [key]));
                return;
            }

            builder.Add(new CommandInstruction(CommandOpCode.GetResource, [key]));
            return;
        }

        if (reader.IsCurrent(SyntaxKind.IdentifierToken))
        {
            var identifier = reader.Consume(SyntaxKind.IdentifierToken);

            // Fallback: treat bare identifiers as strings for now
            builder.Add(new CommandInstruction(CommandOpCode.Push, [identifier.Text]));
            return;
        }

        throw new InvalidOperationException($"Unexpected token '{reader.Current?.Kind ?? SyntaxKind.None}' at '{reader.Current?.Text ?? "null"}'.");
    }

    private static object? GetLiteralValue(SyntaxToken token)
    {
        // Prefer decoded literal value when present
        if (token.Value is not null)
        {
            return token.Value;
        }

        // Fallback to token text
        return token.Text;
    }

    private static string ParseResourceKey(ref TokenReader reader)
    {
        reader.Consume(SyntaxKind.DollarToken);

        var name = reader.ConsumeAny(SyntaxKind.IdentifierToken, SyntaxKind.ParameterKeyword);

        if (name.Kind == SyntaxKind.ParameterKeyword)
        {
            throw new InvalidOperationException("Resource key cannot be '$parameter'.");
        }

        return "$" + name.Text;
    }

    private static bool TryPeekResourceAssignment(ref TokenReader reader, out string resourceKey, out SyntaxKind assignmentOperatorKind)
    {
        resourceKey = string.Empty;
        assignmentOperatorKind = SyntaxKind.None;

        if (!reader.IsCurrent(SyntaxKind.DollarToken))
        {
            return false;
        }

        if (!reader.TryPeek(1, out var nameToken) || (nameToken.Kind is not (SyntaxKind.IdentifierToken or SyntaxKind.ParameterKeyword)))
        {
            return false;
        }

        if (!reader.TryPeek(2, out var opToken))
        {
            return false;
        }

        if (!IsAssignmentOperator(opToken.Kind))
        {
            return false;
        }

        assignmentOperatorKind = opToken.Kind;
        return true;
    }

    private static bool IsAssignmentOperator(SyntaxKind kind)
    {
        return kind is
            SyntaxKind.EqualsToken or
            SyntaxKind.PlusEqualsToken or
            SyntaxKind.MinusEqualsToken or
            SyntaxKind.AsteriskEqualsToken or
            SyntaxKind.SlashEqualsToken or
            SyntaxKind.QuestionQuestionEqualsToken;
    }

    private static CommandOpCode MapCompoundOperatorToOpCode(SyntaxKind assignmentOperatorKind)
    {
        return assignmentOperatorKind switch
        {
            SyntaxKind.PlusEqualsToken => CommandOpCode.Add,
            SyntaxKind.MinusEqualsToken => CommandOpCode.Subtract,
            SyntaxKind.AsteriskEqualsToken => CommandOpCode.Multiply,
            SyntaxKind.SlashEqualsToken => CommandOpCode.Divide,
            _ => throw new InvalidOperationException($"Unsupported compound assignment operator: {assignmentOperatorKind}"),
        };
    }

    private static void EmitSetResource(ImmutableArrayBuilder<CommandInstruction> builder, bool useDirectSetResource, string key, object valueSource)
    {
        if (useDirectSetResource)
        {
            builder.Add(new CommandInstruction(CommandOpCode.DirectSetResource, [key, valueSource]));
            return;
        }

        builder.Add(new CommandInstruction(CommandOpCode.SetResource, [key, valueSource]));
    }

    private static int EmitJump(ImmutableArrayBuilder<CommandInstruction> builder, CommandOpCode opcode)
    {
        var index = builder.Count;
        builder.Add(new CommandInstruction(opcode, [0]));
        return index;
    }

    private static void PatchJump(ImmutableArrayBuilder<CommandInstruction> builder, int instructionIndex, int target)
    {
        builder[instructionIndex] = new CommandInstruction(builder[instructionIndex].OpCode, [target]);
    }

    private struct TokenReader
    {
        private readonly ImmutableArray<SyntaxToken> _tokens;
        private int _position;

        public TokenReader(ImmutableArray<SyntaxToken> tokens)
        {
            _tokens = tokens;
            _position = 0;
        }

        public readonly bool IsAtEnd => _position >= _tokens.Length;

        public readonly SyntaxToken? Current => IsAtEnd ? null : _tokens[_position];

        public readonly bool IsCurrent(SyntaxKind kind) => !IsAtEnd && Current?.Kind == kind;

        public readonly bool TryPeek(int offset, [NotNullWhen(true)] out SyntaxToken? token)
        {
            var index = _position + offset;
            if ((uint)index >= (uint)_tokens.Length)
            {
                token = null;
                return false;
            }

            token = _tokens[index];
            return true;
        }

        public bool TryConsume(SyntaxKind kind)
        {
            if (IsCurrent(kind))
            {
                _position++;
                return true;
            }

            return false;
        }

        public SyntaxToken Consume(SyntaxKind kind)
        {
            if (!IsCurrent(kind))
            {
                throw new InvalidOperationException($"Expected '{kind}' but got '{Current?.Kind}' at '{Current?.Text}'.");
            }

            return _tokens[_position++];
        }

        public void Consume(SyntaxKind kind, bool _)
        {
            Consume(kind);
        }

        public SyntaxToken ConsumeAny(SyntaxKind kind1, SyntaxKind kind2)
        {
            if (IsCurrent(kind1))
            {
                return _tokens[_position++];
            }

            if (IsCurrent(kind2))
            {
                return _tokens[_position++];
            }

            throw new InvalidOperationException($"Expected '{kind1}' or '{kind2}' but got '{Current?.Kind ?? SyntaxKind.None}' at '{Current?.Text ?? "null"}'.");
        }

        public SyntaxToken ConsumeAny(SyntaxKind kind1, SyntaxKind kind2, SyntaxKind kind3)
        {
            if (IsCurrent(kind1))
            {
                return _tokens[_position++];
            }

            if (IsCurrent(kind2))
            {
                return _tokens[_position++];
            }

            if (IsCurrent(kind3))
            {
                return _tokens[_position++];
            }

            throw new InvalidOperationException($"Expected one of '{kind1}', '{kind2}', '{kind3}' but got '{Current?.Kind ?? SyntaxKind.None}' at '{Current?.Text ?? "null"}'.");
        }
    }
}