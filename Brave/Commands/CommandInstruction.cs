using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Brave.Commands;

/// <summary>
/// Instruction with an optional operand (index into ConstantPool).
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly record struct CommandInstruction(CommandOpCode OpCode, Arguments Arguments)
{
    public CommandInstruction(CommandOpCode opCode) : this(opCode, [])
    {

    }

    private string DebuggerDisplay => $"{OpCode} {(Arguments.DebuggerDisplay == "Empty" ? "" : Arguments.DebuggerDisplay)}";


}