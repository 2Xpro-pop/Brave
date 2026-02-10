using Brave.Commands;
using Brave.Compile;
using Brave.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Brave.Tests;

[TestFixture]
public class CompilerTests
{
    private static (IAbstractResources Resources, Dictionary<object, object?> Backing, ImmutableArray<CommandInstruction>) Compile(string source)
    {
        var (resources, backing) = ResourcesMock.CreateResources();
        using var lexer = new Lexer(source);
        var instructions = Compiler.Compile([.. lexer.LexToEnd()]);

        return (resources, backing, instructions);
    }

    private static (IAbstractResources Resources, Dictionary<object, object?> Backing) CompileAndExecute(
        string source,
        object? parameter = null,
        object? owner = null,
        IAbstractResources? parent = null,
        Action<Dictionary<object, object?>>? seed = null)
    {
        var (resources, backing) = ResourcesMock.CreateResources(owner: owner, parent: parent);

        seed?.Invoke(backing);

        using var lexer = new Lexer(source);
        var instructions = Compiler.Compile([.. lexer.LexToEnd()]);

        Interpretator.Execute(resources, parameter ?? new object(), null, instructions);

        return (resources, backing);
    }

    private static void AssertInt64(Dictionary<object, object?> backing, string key, long expected)
    {
        var value = backing[key];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(value, Is.Not.Null);
            Assert.That(Convert.ToInt64(value), Is.EqualTo(expected));
        }
    }

    [Test]
    public void Assignment_Literal_Writes_Resource()
    {
        var (_, backing) = CompileAndExecute("$A = 10;");

        AssertInt64(backing, "$A", 10);
    }

    [Test]
    public void Resource_Read_Then_Assign_Works()
    {
        var (_, backing) = CompileAndExecute("$B = $A;",
            seed: b => b["$A"] = 123);

        AssertInt64(backing, "$B", 123);
    }

    [Test]
    public void Arithmetic_Respects_Precedence()
    {
        var (_, backing) = CompileAndExecute("$R = 1 + 2 * 3;");

        AssertInt64(backing, "$R", 7);
    }

    [Test]
    public void Arithmetic_Parentheses_Override_Precedence()
    {
        var (_, backing) = CompileAndExecute("$R = (1 + 2) * 3;");

        AssertInt64(backing, "$R", 9);
    }

    [Test]
    public void Comparison_And_Ternary_Works()
    {
        // 3 > 4 ? 10 : 20 => 20
        var (_, backing) = CompileAndExecute("$R = 3 > 4 ? 10 : 20;");

        AssertInt64(backing, "$R", 20);
    }

    [Test]
    public void Ternary_DoesNotEvaluate_ElseBranch_When_True()
    {
        // If else branch is evaluated, it will create "$B" via GetOrCreate.
        var (_, backing) = CompileAndExecute("$R = true ? $A : $B;",
            seed: b => b["$A"] = "value");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backing["$R"], Is.EqualTo("value"));
            Assert.That(backing.ContainsKey("$B"), Is.False);
        }
    }

    [Test]
    public void Coalesce_When_Left_NotNull_DoesNotEvaluate_Right()
    {
        // If right branch is evaluated, it will create "$B" via GetOrCreate.
        var (_, backing) = CompileAndExecute("$R = $A ?? $B;",
            seed: b => b["$A"] = "value");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backing["$R"], Is.EqualTo("value"));
            Assert.That(backing.ContainsKey("$B"), Is.False);
        }
    }

    [Test]
    public void Coalesce_When_Left_Null_Evaluates_Right()
    {
        var (_, backing) = CompileAndExecute("$R = $A ?? 5;",
            seed: b => b["$A"] = null);

        AssertInt64(backing, "$R", 5);
    }

    [Test]
    public void LogicalAnd_ShortCircuit_When_Left_False_DoesNotEvaluate_Right()
    {
        // If right side is evaluated, "$B" will be created.
        var (_, backing) = CompileAndExecute("$R = false && $B;");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backing.ContainsKey("$B"), Is.False);
            Assert.That(backing["$R"], Is.False);
        }
    }

    [Test]
    public void LogicalOr_ShortCircuit_When_Left_True_DoesNotEvaluate_Right()
    {
        // If right side is evaluated, "$B" will be created.
        var (_, backing) = CompileAndExecute("$R = true || $B;");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backing.ContainsKey("$B"), Is.False);
            Assert.That(backing["$R"], Is.EqualTo(true));
        }
    }

    [Test]
    public void Increment_And_Decrement_Work()
    {
        // Start at 10: ++ => 11, -- => 10, post++ => leaves 10 but increments to 11, post-- => leaves 11 but decrements to 10
        var (_, backing) = CompileAndExecute("$Counter = 10; ++$Counter; --$Counter; $Counter++; $Counter--;");

        AssertInt64(backing, "$Counter", 10);
    }

    [Test]
    public void Parameter_And_Self_Work()
    {
        var owner = new object();
        var parameter = new object();

        var (resources, backing) = CompileAndExecute("$P = $parameter; $S = $self;",
            parameter: parameter,
            owner: owner);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backing["$P"], Is.SameAs(parameter));
            Assert.That(backing["$S"], Is.SameAs(owner));
        }
    }

    [Test]
    public void Compiler_Produces_Instructions()
    {
        var (_, _, instructions) = Compile("$A = 1 + 2 * 3;");

        Assert.That(instructions.IsDefaultOrEmpty, Is.False);
    }
}
