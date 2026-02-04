using System;
using System.Collections.Generic;
using System.Text;

namespace Brave.Commands;

/// <summary>High-level opcodes (no raw stack fiddling exposed to compiler).</summary>
public enum CommandOpCode : byte
{
    // ---- Special fast paths (superinstructions) ----

    /// <summary>Returns resources[key].</summary>
    ReturnSelf,

    /// <summary>resources[key] = parameter; return assigned value.</summary>
    AssignSelfFromParameterReturn,

    /// <summary>resources[key] = !((bool)resources[key]); return assigned value.</summary>
    ToggleSelfBooleanReturn,

    /// <summary>resources[key] = null; return null.</summary>
    AssignSelfNullReturn,

    /// <summary>resources[key] = constant; return assigned value.</summary>
    AssignSelfFromConstantReturn,

    /// <summary>resources[key] = Convert(constant, selfType); return assigned value.</summary>
    AssignSelfFromConstantConvertedToSelfTypeReturn,

    // ---- Generic expression evaluation (still relatively high-level) ----
    // You can keep these for complex expressions like "$A + 10 == $B".

    LoadSelf,            // push resources[key]
    LoadParameter,       // push parameter
    LoadResourceConstKey,// push resources[constKey]
    PushConstant,        // push constant
    Not,
    Add,
    Subtract,
    Multiply,
    Divide,
    Equal,

    ConvertTopToSelfType,

    StoreSelf,           // resources[key] = pop; push assigned value
    Return,              // return top
}