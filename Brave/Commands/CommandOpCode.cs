using System;
using System.Collections.Generic;
using System.Text;

namespace Brave.Commands;

/// <summary>
/// High-level opcodes (no raw stack fiddling exposed to compiler).
/// </summary>
public enum CommandOpCode : byte
{
    Push,
    GetResource,
    SetResource,
    DirectSetResource,

    PreIncrementResource,
    PostIncrementResource,

    PreDecrementResource,
    PostDecrementResource,

    PushParameter,
    PushSelf,

    InvokeResource,

    IndexGet,
    IndexSet,

    // Unary
    Negate,
    LogicalNot,
    BitwiseNot,

    // Arithmetic
    Add,
    Subtract,
    Multiply,
    Divide,

    // Logical (strict; no short-circuit yet)
    LogicalAnd,
    LogicalOr,

    // Bitwise
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,

    // Comparison
    Equal,
    NotEqual,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,

    // -------- Control Flow --------
    Jump,
    JumpIfFalse,
    JumpIfTrue,
    JumpIfNull,

    JumpIfNotNull,
}