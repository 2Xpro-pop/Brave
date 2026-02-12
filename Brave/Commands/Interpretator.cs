using Brave.Compile;
using Brave.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Windows.Input;

namespace Brave.Commands;

internal static class Interpretator
{
    public static readonly object Void = new();

    public static object Execute(IAbstractResources resources, object? parameter, IMetaInfoProvider? metaInfoProvider, string expression, bool useDirectResources = false)
    {
        var instructions = Compiler.Compile(expression, useDirectResources);

        return Execute(resources, parameter, metaInfoProvider, instructions);
    }

    public static object Execute(IAbstractResources resources, object? parameter, IMetaInfoProvider? metaInfoProvider, ImmutableArray<CommandInstruction> commandInstructions)
    {
        object? key = null;
        object? value = null;
        object? nextValue = null;
        object? target = null;
        object? index = null;
        object? collection = null;

        using var runtimeStack = new RuntimeStack();

        for (int i = 0; i < commandInstructions.Length; i++)
        {
            CommandInstruction instruction = commandInstructions[i];

            var opcode = instruction.OpCode;
            var arguments = instruction.Arguments;

            switch (opcode)
            {
                case CommandOpCode.Push:
                    value = arguments[0];
                    runtimeStack.Push(value);
                    break;

                case CommandOpCode.SetResource:
                    key = arguments[0];
                    value = arguments[1];

                    resources.TrySetToExistingKey(key, runtimeStack.PopOrReturn(value));
                    break;

                case CommandOpCode.DirectSetResource:
                    key = arguments[0];
                    value = arguments[1];

                    resources[key] = runtimeStack.PopOrReturn(value);
                    break;

                case CommandOpCode.GetResource:
                    key = arguments[0];
                    value = resources.GetOrCreate(key);

                    runtimeStack.Push(value);
                    break;

                case CommandOpCode.PreIncrementResource:
                    key = arguments[0];
                    value = arguments[1];

                    value = Increment(resources.GetOrCreate(key, value));
                    resources.TrySetToExistingKey(key, value);

                    runtimeStack.Push(value);

                    break;

                case CommandOpCode.PostIncrementResource:
                    key = arguments[0];
                    value = resources.GetOrCreate(key, Boxes.BoxedInt0);

                    nextValue = Increment(value);
                    resources.TrySetToExistingKey(key, nextValue);

                    runtimeStack.Push(value);
                    break;


                case CommandOpCode.PreDecrementResource:
                    key = arguments[0];
                    value = resources.GetOrCreate(key, Boxes.BoxedInt0);

                    value = Decrement(value);
                    resources.TrySetToExistingKey(key, value);

                    runtimeStack.Push(value);
                    break;

                case CommandOpCode.PostDecrementResource:
                    key = arguments[0];
                    value = resources.GetOrCreate(key, Boxes.BoxedInt0);

                    nextValue = Decrement(value);
                    resources.TrySetToExistingKey(key, nextValue);

                    runtimeStack.Push(value);
                    break;

                case CommandOpCode.PushParameter:
                    runtimeStack.Push(parameter);
                    break;

                case CommandOpCode.PushSelf:
                    runtimeStack.Push(metaInfoProvider?.CurrentOrRootObject ?? resources.Owner);
                    break;

                case CommandOpCode.InvokeResource:
                    key = arguments[0];
                    value = runtimeStack.Pop();

                    var command = resources.GetOrCreate(key);

                    if (command is not ICommand cmd)
                    {
                        throw new InvalidOperationException($"Resource '{key}' is not a command.");
                    }

                    value = runtimeStack.PopOrReturn(value);

                    cmd.Execute(value);
                    break;

                // -------- Indexing --------

                case CommandOpCode.IndexGet:
                    key = arguments[0];
                    index = runtimeStack.Pop();      // index
                    collection = resources.GetOrCreate(key);

                    if(collection is IDictionary)
                    {
                        value = ((IDictionary)collection)[index];
                    }
                    else if(collection is IList list && index is int intIndex)
                    {
                        value = list[intIndex];
                    }
                    else
                    {
                        throw new InvalidOperationException($"Resource '{key}' is not indexable.");
                    }

                    runtimeStack.Push(value);

                    break;

                case CommandOpCode.IndexSet:
                    key = arguments[0];
                    index = runtimeStack.Pop();      // index
                    value = runtimeStack.Pop();      // value
                    collection = resources.GetOrCreate(key);

                    if (collection is IDictionary)
                    {
                        ((IDictionary)collection)[index] = value;
                    }
                    else if (collection is IList list && index is int intIndex)
                    {
                        list[intIndex] = value;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Resource '{key}' is not indexable.");
                    }

                    runtimeStack.Push(value);

                    break;

                // -------- Unary --------

                case CommandOpCode.Negate:
                    value = runtimeStack.Pop();
                    runtimeStack.Push(Negate(value));
                    break;

                case CommandOpCode.LogicalNot:
                    value = runtimeStack.Pop();
                    runtimeStack.Push(!ToBoolean(value));
                    break;

                case CommandOpCode.BitwiseNot:
                    value = runtimeStack.Pop();
                    runtimeStack.Push(BitwiseNot(value));
                    break;

                // -------- Arithmetic --------

                case CommandOpCode.Add:
                    value = runtimeStack.Pop();      // right
                    nextValue = runtimeStack.Pop();  // left
                    runtimeStack.Push(Add(nextValue, value));
                    break;

                case CommandOpCode.Subtract:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(Subtract(nextValue, value));
                    break;

                case CommandOpCode.Multiply:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(Multiply(nextValue, value));
                    break;

                case CommandOpCode.Divide:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(Divide(nextValue, value));
                    break;

                // -------- Logical (strict) --------

                case CommandOpCode.LogicalAnd:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(ToBoolean(nextValue) && ToBoolean(value));
                    break;

                case CommandOpCode.LogicalOr:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(ToBoolean(nextValue) || ToBoolean(value));
                    break;

                // -------- Bitwise --------

                case CommandOpCode.BitwiseAnd:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(BitwiseAnd(nextValue, value));
                    break;

                case CommandOpCode.BitwiseOr:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(BitwiseOr(nextValue, value));
                    break;

                case CommandOpCode.BitwiseXor:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(BitwiseXor(nextValue, value));
                    break;

                // -------- Comparison --------

                case CommandOpCode.Equal:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(Equals(nextValue, value));
                    break;

                case CommandOpCode.NotEqual:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(!Equals(nextValue, value));
                    break;

                case CommandOpCode.GreaterThan:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(Compare(nextValue, value) > 0);
                    break;

                case CommandOpCode.GreaterOrEqual:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(Compare(nextValue, value) >= 0);
                    break;

                case CommandOpCode.LessThan:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(Compare(nextValue, value) < 0);
                    break;

                case CommandOpCode.LessOrEqual:
                    value = runtimeStack.Pop();
                    nextValue = runtimeStack.Pop();
                    runtimeStack.Push(Compare(nextValue, value) <= 0);
                    break;

                // -------- Control Flow --------

                case CommandOpCode.Jump:
                    target = (int)arguments[0];

                    if ((uint)(int)target > (uint)commandInstructions.Length)
                    {
                        throw new InvalidOperationException($"Jump target out of range: {target}");
                    }

                    i = (int)target - 1;
                    break;

                case CommandOpCode.JumpIfFalse:
                    target = (int)arguments[0];

                    if ((uint)(int)target >= (uint)commandInstructions.Length)
                    {
                        throw new InvalidOperationException($"JumpIfFalse target out of range: {target}");
                    }

                    value = runtimeStack.Pop();

                    if (!ToBoolean(value))
                    {
                        i = (int)target - 1;
                    }

                    break;

                case CommandOpCode.JumpIfTrue:
                    target = (int)arguments[0];

                    if ((uint)(int)target >= (uint)commandInstructions.Length)
                    {
                        throw new InvalidOperationException($"JumpIfTrue target out of range: {target}");
                    }

                    value = runtimeStack.Pop();

                    if (ToBoolean(value))
                    {
                        i = (int)target - 1;
                    }

                    break;

                case CommandOpCode.JumpIfNull:
                    target = (int)arguments[0];

                    if ((uint)(int)target >= (uint)commandInstructions.Length)
                    {
                        throw new InvalidOperationException($"JumpIfNull target out of range: {target}");
                    }

                    value = runtimeStack.Peek();

                    if (value is null)
                    {
                        runtimeStack.Pop();
                        i = (int)target - 1;
                    }

                    break;

                case CommandOpCode.JumpIfNotNull:
                    target = (int)arguments[0];

                    if ((uint)(int)target >= (uint)commandInstructions.Length)
                    {
                        throw new InvalidOperationException($"JumpIfNotNull target out of range: {target}");
                    }

                    value = runtimeStack.Peek();

                    if (value is not null)
                    {
                        i = (int)target - 1;
                    }

                    break;

                default:
                    throw new InvalidOperationException($"Unknown opcode: {opcode}");
            }

        }

        return runtimeStack.Count > 0 ? runtimeStack.Pop()! : Void;
    }

    private static object Increment(object? value)
    {
        // Minimal numeric support for now (int/double/long/decimal).
        // You can expand later.
        if (value is null)
        {
            return 1;
        }

        return value switch
        {
            int intValue => Boxes.Box(intValue + 1),
            long longValue => Boxes.Box(longValue + 1),
            double doubleValue => Boxes.Box(doubleValue + 1d),
            float floatValue => Boxes.Box(floatValue + 1f),
            decimal decimalValue => Boxes.Box(decimalValue + 1m),
            _ => throw new InvalidOperationException($"Cannot increment value of type '{value.GetType().FullName}'.")
        };
    }

    private static object Decrement(object? value)
    {
        if (value is null)
        {
            return Boxes.Box(-1);
        }

        return value switch
        {
            int intValue => Boxes.Box(intValue - 1),
            long longValue => Boxes.Box(longValue - 1),
            double doubleValue => Boxes.Box(doubleValue - 1d),
            float floatValue => Boxes.Box(floatValue - 1f),
            decimal decimalValue => Boxes.Box(decimalValue - 1m),
            _ => throw new InvalidOperationException($"Cannot decrement value of type '{value.GetType().FullName}'.")
        };
    }

    private static bool ToBoolean(object? value)
    {
        if (value is null)
        {
            return false;
        }

        return value switch
        {
            bool boolValue => boolValue,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            double doubleValue => doubleValue != 0d,
            float floatValue => floatValue != 0f,
            decimal decimalValue => decimalValue != 0m,
            string stringValue => bool.TryParse(stringValue, out var boolValue) && boolValue,
            _ => throw new InvalidOperationException($"Cannot convert '{value.GetType().FullName}' to boolean.")
        };
    }

    private static object Negate(object? value)
    {
        if (value is null)
        {
            return Boxes.BoxedInt0;
        }

        return value switch
        {
            int intValue => Boxes.Box(-intValue),
            long longValue => Boxes.Box(-longValue),
            double doubleValue => Boxes.Box(-doubleValue),
            float floatValue => Boxes.Box(-floatValue),
            decimal decimalValue => Boxes.Box(-decimalValue),
            _ => throw new InvalidOperationException($"Cannot negate value of type '{value.GetType().FullName}'.")
        };
    }

    private static object Add(object? left, object? right)
    {
        var leftString = left as string;
        var rightString = right as string;

        if (left is string || right is string)
        {
            return (leftString ?? left?.ToString() ?? string.Empty) + (rightString ?? right?.ToString() ?? string.Empty);
        }

        return AddNumeric(left, right);
    }

    private static object Subtract(object? left, object? right) => SubtractNumeric(left, right);
    private static object Multiply(object? left, object? right) => MultiplyNumeric(left, right);
    private static object Divide(object? left, object? right) => DivideNumeric(left, right);

    private static object AddNumeric(object? left, object? right)
    {
        if (left is decimal || right is decimal)
        {
            var l = left is null ? 0m : Convert.ToDecimal(left);
            var r = right is null ? 0m : Convert.ToDecimal(right);
            return Boxes.Box(l + r);
        }

        if (left is double || right is double)
        {
            var l = left is null ? 0d : Convert.ToDouble(left);
            var r = right is null ? 0d : Convert.ToDouble(right);
            return Boxes.Box(l + r);
        }

        if (left is float || right is float)
        {
            var l = left is null ? 0f : Convert.ToSingle(left);
            var r = right is null ? 0f : Convert.ToSingle(right);
            return Boxes.Box(l + r);
        }

        if (left is ulong || right is ulong)
        {
            var l = left is null ? 0ul : Convert.ToUInt64(left);
            var r = right is null ? 0ul : Convert.ToUInt64(right);
            return Boxes.Box(l + r);
        }

        if (left is long || right is long)
        {
            var l = left is null ? 0L : Convert.ToInt64(left);
            var r = right is null ? 0L : Convert.ToInt64(right);
            return Boxes.Box(l + r);
        }

        if (left is uint || right is uint)
        {
            var l = left is null ? 0u : Convert.ToUInt32(left);
            var r = right is null ? 0u : Convert.ToUInt32(right);
            return Boxes.Box(l + r);
        }

        {
            var l = left is null ? 0 : Convert.ToInt32(left);
            var r = right is null ? 0 : Convert.ToInt32(right);
            return Boxes.Box(l + r);
        }
    }

    private static object SubtractNumeric(object? left, object? right)
    {
        if (left is decimal || right is decimal)
        {
            var l = left is null ? 0m : Convert.ToDecimal(left);
            var r = right is null ? 0m : Convert.ToDecimal(right);
            return Boxes.Box(l - r);
        }

        if (left is double || right is double)
        {
            var l = left is null ? 0d : Convert.ToDouble(left);
            var r = right is null ? 0d : Convert.ToDouble(right);
            return Boxes.Box(l - r);
        }

        if (left is float || right is float)
        {
            var l = left is null ? 0f : Convert.ToSingle(left);
            var r = right is null ? 0f : Convert.ToSingle(right);
            return Boxes.Box(l - r);
        }

        if (left is ulong || right is ulong)
        {
            var l = left is null ? 0ul : Convert.ToUInt64(left);
            var r = right is null ? 0ul : Convert.ToUInt64(right);
            return Boxes.Box(l - r);
        }

        if (left is long || right is long)
        {
            var l = left is null ? 0L : Convert.ToInt64(left);
            var r = right is null ? 0L : Convert.ToInt64(right);
            return Boxes.Box(l - r);
        }

        if (left is uint || right is uint)
        {
            var l = left is null ? 0u : Convert.ToUInt32(left);
            var r = right is null ? 0u : Convert.ToUInt32(right);
            return Boxes.Box(l - r);
        }

        {
            var l = left is null ? 0 : Convert.ToInt32(left);
            var r = right is null ? 0 : Convert.ToInt32(right);
            return Boxes.Box(l - r);
        }
    }

    private static object MultiplyNumeric(object? left, object? right)
    {
        if (left is decimal || right is decimal)
        {
            var l = left is null ? 0m : Convert.ToDecimal(left);
            var r = right is null ? 0m : Convert.ToDecimal(right);
            return Boxes.Box(l * r);
        }

        if (left is double || right is double)
        {
            var l = left is null ? 0d : Convert.ToDouble(left);
            var r = right is null ? 0d : Convert.ToDouble(right);
            return Boxes.Box(l * r);
        }

        if (left is float || right is float)
        {
            var l = left is null ? 0f : Convert.ToSingle(left);
            var r = right is null ? 0f : Convert.ToSingle(right);
            return Boxes.Box(l * r);
        }

        if (left is ulong || right is ulong)
        {
            var l = left is null ? 0ul : Convert.ToUInt64(left);
            var r = right is null ? 0ul : Convert.ToUInt64(right);
            return Boxes.Box(l * r);
        }

        if (left is long || right is long)
        {
            var l = left is null ? 0L : Convert.ToInt64(left);
            var r = right is null ? 0L : Convert.ToInt64(right);
            return Boxes.Box(l * r);
        }

        if (left is uint || right is uint)
        {
            var l = left is null ? 0u : Convert.ToUInt32(left);
            var r = right is null ? 0u : Convert.ToUInt32(right);
            return Boxes.Box(l * r);
        }

        {
            var l = left is null ? 0 : Convert.ToInt32(left);
            var r = right is null ? 0 : Convert.ToInt32(right);
            return Boxes.Box(l * r);
        }
    }

    private static object DivideNumeric(object? left, object? right)
    {
        if (left is decimal || right is decimal)
        {
            var l = left is null ? 0m : Convert.ToDecimal(left);
            var r = right is null ? 0m : Convert.ToDecimal(right);

            if (r == 0m)
                throw new DivideByZeroException();

            return Boxes.Box(l / r);
        }

        if (left is double || right is double)
        {
            var l = left is null ? 0d : Convert.ToDouble(left);
            var r = right is null ? 0d : Convert.ToDouble(right);

            if (r == 0d)
                throw new DivideByZeroException();

            return Boxes.Box(l / r);
        }

        if (left is float || right is float)
        {
            var l = left is null ? 0f : Convert.ToSingle(left);
            var r = right is null ? 0f : Convert.ToSingle(right);

            if (r == 0f)
                throw new DivideByZeroException();

            return Boxes.Box(l / r);
        }

        if (left is ulong || right is ulong)
        {
            var l = left is null ? 0ul : Convert.ToUInt64(left);
            var r = right is null ? 0ul : Convert.ToUInt64(right);

            if (r == 0ul)
                throw new DivideByZeroException();

            return Boxes.Box(l / r);
        }

        if (left is long || right is long)
        {
            var l = left is null ? 0L : Convert.ToInt64(left);
            var r = right is null ? 0L : Convert.ToInt64(right);

            if (r == 0L)
                throw new DivideByZeroException();

            return Boxes.Box(l / r);
        }

        if (left is uint || right is uint)
        {
            var l = left is null ? 0u : Convert.ToUInt32(left);
            var r = right is null ? 0u : Convert.ToUInt32(right);

            if (r == 0u)
                throw new DivideByZeroException();

            return Boxes.Box(l / r);
        }
        {
            var l = left is null ? 0 : Convert.ToInt32(left);
            var r = right is null ? 0 : Convert.ToInt32(right);

            if (r == 0)
                throw new DivideByZeroException();

            return Boxes.Box(l / r);
        }
    }
    private static object BitwiseNot(object? value)
    {
        if (value is null)
        {
            return Boxes.BoxedInt0;
        }

        return value switch
        {
            int intValue => Boxes.Box(~intValue),
            long longValue => Boxes.Box(~longValue),
            uint uintValue => Boxes.Box(~uintValue),
            ulong ulongValue => Boxes.Box(~ulongValue),
            bool boolValue => Boxes.Box(!boolValue),
            _ => throw new InvalidOperationException($"Cannot apply bitwise not to '{value.GetType().FullName}'.")
        };
    }

    private static object BitwiseAnd(object? left, object? right)
    {
        if (left is bool leftBool && right is bool rightBool)
        {
            return Boxes.Box(leftBool & rightBool);
        }

        if (left is ulong || right is ulong || left is uint || right is uint)
        {
            var l = left is null ? 0ul : Convert.ToUInt64(left);
            var r = right is null ? 0ul : Convert.ToUInt64(right);
            return Boxes.Box(l & r);
        }

        {
            var l = left is null ? 0L : Convert.ToInt64(left);
            var r = right is null ? 0L : Convert.ToInt64(right);
            return Boxes.Box(l & r);
        }
    }

    private static object BitwiseOr(object? left, object? right)
    {
        if (left is bool leftBool && right is bool rightBool)
        {
            return Boxes.Box(leftBool | rightBool);
        }

        if (left is ulong || right is ulong || left is uint || right is uint)
        {
            var l = left is null ? 0ul : Convert.ToUInt64(left);
            var r = right is null ? 0ul : Convert.ToUInt64(right);
            return Boxes.Box(l | r);
        }

        {
            var l = left is null ? 0L : Convert.ToInt64(left);
            var r = right is null ? 0L : Convert.ToInt64(right);
            return Boxes.Box(l | r);
        }
    }

    private static object BitwiseXor(object? left, object? right)
    {
        if (left is bool leftBool && right is bool rightBool)
        {
            return Boxes.Box(leftBool ^ rightBool);
        }

        if (left is ulong || right is ulong || left is uint || right is uint)
        {
            var l = left is null ? 0ul : Convert.ToUInt64(left);
            var r = right is null ? 0ul : Convert.ToUInt64(right);
            return Boxes.Box(l ^ r);
        }

        {
            var l = left is null ? 0L : Convert.ToInt64(left);
            var r = right is null ? 0L : Convert.ToInt64(right);
            return Boxes.Box(l ^ r);
        }
    }

    private static int Compare(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left is decimal || right is decimal)
        {
            var l = Convert.ToDecimal(left);
            var r = Convert.ToDecimal(right);
            return l.CompareTo(r);
        }

        if (left is double || right is double || left is float || right is float)
        {
            var l = Convert.ToDouble(left);
            var r = Convert.ToDouble(right);
            return l.CompareTo(r);
        }

        if (left is ulong || right is ulong || left is uint || right is uint)
        {
            var l = Convert.ToUInt64(left);
            var r = Convert.ToUInt64(right);
            return l.CompareTo(r);
        }

        {
            var l = Convert.ToInt64(left);
            var r = Convert.ToInt64(right);
            return l.CompareTo(r);
        }
    }

}
