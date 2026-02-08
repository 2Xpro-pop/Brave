using Brave.Commands;
using Brave.Syntax;
using NSubstitute;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Brave.Tests;

[TestFixture]
public sealed class InterpretatorTests
{
    [Test]
    public void Jump_Skips_Instruction()
    {
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, [1]),
            new CommandInstruction(CommandOpCode.Jump, [3]),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["X", RuntimeStack.Indexes.Last]), // skipped
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Y", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary.ContainsKey("X"), Is.False);
            Assert.That(backingDictionary["Y"], Is.EqualTo(1));
        }
    }

    [Test]
    public void Ternary_When_False_Chooses_ElseBranch()
    {
        // false ? "T" : "F"
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, [false]),
            new CommandInstruction(CommandOpCode.JumpIfFalse, [4]),
            new CommandInstruction(CommandOpCode.Push, ["T"]),
            new CommandInstruction(CommandOpCode.Jump, [5]),
            new CommandInstruction(CommandOpCode.Push, ["F"]),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Result", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        Assert.That(backingDictionary["Result"], Is.EqualTo("F"));
    }

    [Test]
    public void Ternary_When_True_Chooses_ThenBranch()
    {
        // true ? "T" : "F"
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, [true]),
            new CommandInstruction(CommandOpCode.JumpIfFalse, [4]),
            new CommandInstruction(CommandOpCode.Push, ["T"]),
            new CommandInstruction(CommandOpCode.Jump, [5]),
            new CommandInstruction(CommandOpCode.Push, ["F"]),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Result", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        Assert.That(backingDictionary["Result"], Is.EqualTo("T"));
    }

    [Test]
    public void Coalesce_When_Left_NotNull_Returns_Left()
    {
        // A ?? "fallback"
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        backingDictionary["A"] = "value";

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.GetResource, ["A"]),           // push A
            new CommandInstruction(CommandOpCode.JumpIfNull, [3]),              // if null -> pop and jump to fallback
            new CommandInstruction(CommandOpCode.Jump, [4]),                    // if not null -> skip fallback
            new CommandInstruction(CommandOpCode.Push, ["fallback"]),           // fallback
            new CommandInstruction(CommandOpCode.DirectSetResource, ["R", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        Assert.That(backingDictionary["R"], Is.EqualTo("value"));
    }

    [Test]
    public void Coalesce_When_Left_Null_Returns_Fallback()
    {
        // A ?? "fallback"
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        backingDictionary["A"] = null;

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.GetResource, ["A"]),           // push A(null)
            new CommandInstruction(CommandOpCode.JumpIfNull, [3]),              // pop null and jump to fallback
            new CommandInstruction(CommandOpCode.Jump, [4]),                    // skip fallback
            new CommandInstruction(CommandOpCode.Push, ["fallback"]),           // fallback
            new CommandInstruction(CommandOpCode.DirectSetResource, ["R", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        Assert.That(backingDictionary["R"], Is.EqualTo("fallback"));
    }

    [Test]
    public void LogicalAnd_ShortCircuit_When_Left_False_DoesNotEvaluate_Right()
    {
        // left && right, but "right" has side effect: Hit = true
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, [false]),                        // left
            new CommandInstruction(CommandOpCode.JumpIfFalse, [7]),                     // if left false -> jump to push false

            new CommandInstruction(CommandOpCode.DirectSetResource, ["Hit", true]),     // side effect (must be skipped)
            new CommandInstruction(CommandOpCode.Push, [true]),                         // right value
            new CommandInstruction(CommandOpCode.JumpIfFalse, [7]),                     // if right false -> push false

            new CommandInstruction(CommandOpCode.Push, [true]),
            new CommandInstruction(CommandOpCode.Jump, [8]),

            new CommandInstruction(CommandOpCode.Push, [false]),                        // 7
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Result", RuntimeStack.Indexes.Last]) // 8
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        Assert.That(backingDictionary.ContainsKey("Hit"), Is.False);
        Assert.That(backingDictionary["Result"], Is.EqualTo(false));
    }

    [Test]
    public void LogicalOr_ShortCircuit_When_Left_True_DoesNotEvaluate_Right()
    {
        // left || right, but "right" has side effect: Hit = true
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, [true]),                         // left
            new CommandInstruction(CommandOpCode.JumpIfTrue, [7]),                      // if left true -> jump to push true

            new CommandInstruction(CommandOpCode.DirectSetResource, ["Hit", true]),     // side effect (must be skipped)
            new CommandInstruction(CommandOpCode.Push, [false]),                        // right value
            new CommandInstruction(CommandOpCode.JumpIfTrue, [7]),                      // if right true -> push true

            new CommandInstruction(CommandOpCode.Push, [false]),
            new CommandInstruction(CommandOpCode.Jump, [8]),

            new CommandInstruction(CommandOpCode.Push, [true]),                         // 7
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Result", RuntimeStack.Indexes.Last]) // 8
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary.ContainsKey("Hit"), Is.False);
            Assert.That(backingDictionary["Result"], Is.True);
        }
    }

    [Test]
    public void PushParameter_PushSelf_Works()
    {
        var owner = new object();
        var parameter = new object();

        var (resources, backingDictionary) = ResourcesMock.CreateResources(owner: owner);

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.PushParameter),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["P", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.PushSelf),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["S", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary["P"], Is.SameAs(parameter));
            Assert.That(backingDictionary["S"], Is.SameAs(owner));
        }
    }

    [Test]
    public void SetResource_Sets_To_Existing_Key_In_Parent()
    {
        var (parent, parentBacking) = ResourcesMock.CreateResources();
        parentBacking["A"] = 10;

        var (resources, backingDictionary) = ResourcesMock.CreateResources(parent: parent);
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, [123]),
            new CommandInstruction(CommandOpCode.SetResource, ["A", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parentBacking["A"], Is.EqualTo(123));
            Assert.That(backingDictionary.ContainsKey("A"), Is.False);
        }
    }

    [Test]
    public void JumpIfNotNull_Jumps_When_Value_NotNull()
    {
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, ["X"]),
            new CommandInstruction(CommandOpCode.JumpIfNotNull, [4]),

            new CommandInstruction(CommandOpCode.DirectSetResource, ["Hit", true]), // skipped
            new CommandInstruction(CommandOpCode.Jump, [5]),

            new CommandInstruction(CommandOpCode.DirectSetResource, ["Result", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary.ContainsKey("Hit"), Is.False);
            Assert.That(backingDictionary["Result"], Is.EqualTo("X"));
        }
    }

    [Test]
    public void PreIncrementResource_Returns_Incremented_Value()
    {
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        backingDictionary["C"] = 10;

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.PreIncrementResource, ["C", Boxes.BoxedInt0]),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["R", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary["C"], Is.EqualTo(11));
            Assert.That(backingDictionary["R"], Is.EqualTo(11));
        }
    }

    [Test]
    public void PostIncrementResource_Returns_Old_Value()
    {
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        backingDictionary["C"] = 10;

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.PostIncrementResource, ["C"]),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["R", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary["C"], Is.EqualTo(11));
            Assert.That(backingDictionary["R"], Is.EqualTo(10));
        }
    }

    [Test]
    public void PreDecrementResource_Returns_Decremented_Value()
    {
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        backingDictionary["C"] = 10;

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.PreDecrementResource, ["C"]),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["R", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary["C"], Is.EqualTo(9));
            Assert.That(backingDictionary["R"], Is.EqualTo(9));
        }
    }

    [Test]
    public void PostDecrementResource_Returns_Old_Value()
    {
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        backingDictionary["C"] = 10;

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.PostDecrementResource, ["C"]),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["R", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary["C"], Is.EqualTo(9));
            Assert.That(backingDictionary["R"], Is.EqualTo(10));
        }
    }

    [Test]
    public void Unary_Negate_LogicalNot_BitwiseNot_Works()
    {
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, [10]),
            new CommandInstruction(CommandOpCode.Negate),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Neg", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [true]),
            new CommandInstruction(CommandOpCode.LogicalNot),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Not", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [0b_1010]),
            new CommandInstruction(CommandOpCode.BitwiseNot),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["BitNot", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary["Neg"], Is.EqualTo(-10));
            Assert.That(backingDictionary["Not"], Is.False);
            Assert.That(backingDictionary["BitNot"], Is.EqualTo(~0b_1010));
        }
    }

    [Test]
    public void Arithmetic_Add_Subtract_Multiply_Divide_Works()
    {
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, [7]),
            new CommandInstruction(CommandOpCode.Push, [3]),
            new CommandInstruction(CommandOpCode.Add),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Add", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [7]),
            new CommandInstruction(CommandOpCode.Push, [3]),
            new CommandInstruction(CommandOpCode.Subtract),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Sub", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [7]),
            new CommandInstruction(CommandOpCode.Push, [3]),
            new CommandInstruction(CommandOpCode.Multiply),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Mul", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [8]),
            new CommandInstruction(CommandOpCode.Push, [2]),
            new CommandInstruction(CommandOpCode.Divide),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Div", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary["Add"], Is.EqualTo(10));
            Assert.That(backingDictionary["Sub"], Is.EqualTo(4));
            Assert.That(backingDictionary["Mul"], Is.EqualTo(21));
            Assert.That(backingDictionary["Div"], Is.EqualTo(4));
        }
    }

    [Test]
    public void Logical_Strict_And_Or_Works()
    {
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, [true]),
            new CommandInstruction(CommandOpCode.Push, [false]),
            new CommandInstruction(CommandOpCode.LogicalAnd),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["And", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [true]),
            new CommandInstruction(CommandOpCode.Push, [false]),
            new CommandInstruction(CommandOpCode.LogicalOr),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Or", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary["And"], Is.False);
            Assert.That(backingDictionary["Or"], Is.True);
        }
    }

    [Test]
    public void Bitwise_And_Or_Xor_Works()
    {
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, [0b_1100]),
            new CommandInstruction(CommandOpCode.Push, [0b_1010]),
            new CommandInstruction(CommandOpCode.BitwiseAnd),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["And", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [0b_1100]),
            new CommandInstruction(CommandOpCode.Push, [0b_1010]),
            new CommandInstruction(CommandOpCode.BitwiseOr),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Or", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [0b_1100]),
            new CommandInstruction(CommandOpCode.Push, [0b_1010]),
            new CommandInstruction(CommandOpCode.BitwiseXor),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Xor", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary["And"], Is.EqualTo(0b_1000));
            Assert.That(backingDictionary["Or"], Is.EqualTo(0b_1110));
            Assert.That(backingDictionary["Xor"], Is.EqualTo(0b_0110));
        }
    }

    [Test]
    public void Comparison_Operators_Work()
    {
        var (resources, backingDictionary) = ResourcesMock.CreateResources();
        var parameter = new object();

        var commandInstructions = ImmutableArray.Create(
            new CommandInstruction(CommandOpCode.Push, [5]),
            new CommandInstruction(CommandOpCode.Push, [5]),
            new CommandInstruction(CommandOpCode.Equal),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Eq", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [5]),
            new CommandInstruction(CommandOpCode.Push, [6]),
            new CommandInstruction(CommandOpCode.NotEqual),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Ne", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [6]),
            new CommandInstruction(CommandOpCode.Push, [5]),
            new CommandInstruction(CommandOpCode.GreaterThan),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Gt", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [6]),
            new CommandInstruction(CommandOpCode.Push, [6]),
            new CommandInstruction(CommandOpCode.GreaterOrEqual),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Ge", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [5]),
            new CommandInstruction(CommandOpCode.Push, [6]),
            new CommandInstruction(CommandOpCode.LessThan),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Lt", RuntimeStack.Indexes.Last]),

            new CommandInstruction(CommandOpCode.Push, [5]),
            new CommandInstruction(CommandOpCode.Push, [5]),
            new CommandInstruction(CommandOpCode.LessOrEqual),
            new CommandInstruction(CommandOpCode.DirectSetResource, ["Le", RuntimeStack.Indexes.Last])
        );

        Interpretator.Execute(resources, parameter, commandInstructions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backingDictionary["Eq"], Is.True);
            Assert.That(backingDictionary["Ne"], Is.True);
            Assert.That(backingDictionary["Gt"], Is.True);
            Assert.That(backingDictionary["Ge"], Is.True);
            Assert.That(backingDictionary["Lt"], Is.True);
            Assert.That(backingDictionary["Le"], Is.True);
        }
    }
}