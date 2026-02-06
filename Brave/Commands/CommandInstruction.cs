using System;
using System.Collections.Generic;
using System.Text;

namespace Brave.Commands;

/// <summary>
/// Instruction with an optional operand (index into ConstantPool).
/// </summary>
public readonly record struct CommandInstruction(CommandOpCode OpCode, Arguments Arguments);