using Brave.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brave.Tests;

[TestFixture]
internal class InterpretatorExpressionReturnTests
{
    private static object? Execute(
        string expression,
        out Dictionary<object, object?> backing,
        bool useDirectResources = true,
        object? parameter = null,
        object? owner = null,
        IAbstractResources? parent = null)
    {
        (var resources, backing) = ResourcesMock.CreateResources(owner: owner, parent: parent);

        return Interpretator.Execute(resources, parameter, expression, useDirectResources);
    }

    private static void AssertResult(string expression, object? expected, bool useDirectResources = true)
    {
        var result = Execute(expression, out var _, useDirectResources);

        Assert.That(result, Is.EqualTo(expected), $"Expression: {expression}");
    }

    [Test]
    public void Arithmetic_Returns_Value()
        => AssertResult("2 + 11", 13);

    [Test]
    public void Precedence_Multiply_Before_Add()
        => AssertResult("2 + 3 * 4", 14);

    [Test]
    public void Parentheses_Override_Precedence()
        => AssertResult("(2 + 3) * 4", 20);

    [Test]
    public void Null_Literal_Returns_Null()
    {
        var result = Execute("null", out var _);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Bare_Identifier_Is_Treated_As_String()
        => AssertResult("hello", "hello");

    [Test]
    public void Ternary_Returns_Then_When_True()
        => AssertResult("true ? 10 : 20", 10);

    [Test]
    public void Ternary_Returns_Else_When_False()
        => AssertResult("false ? 10 : 20", 20);

    [Test]
    public void Coalesce_Returns_Left_When_NotNull()
        => AssertResult("\"x\" ?? \"fallback\"", "x");

    [Test]
    public void Coalesce_Is_Right_Associative()
        => AssertResult("null ?? null ?? 7", 7);

    [Test]
    public void Assignment_Returns_Assigned_Value_And_Sets_Resource()
    {
        var result = Execute("$x = 7", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(7));
            Assert.That(backing["$x"], Is.EqualTo(7));
        }
    }

    [Test]
    public void Sequence_Returns_Last_Expression_Result()
    {
        var result = Execute("$a = 1; $b = 2; $a + $b", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(3));
            Assert.That(backing["$a"], Is.EqualTo(1));
            Assert.That(backing["$b"], Is.EqualTo(2));
        }
    }

    [Test]
    public void Compound_Assignment_PlusEquals_Returns_Assigned_Value()
    {
        var result = Execute("$x = 10; $x += 5", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(15));
            Assert.That(backing["$x"], Is.EqualTo(15));
        }
    }

    [Test]
    public void Compound_Assignment_MultiplyEquals_Works()
    {
        var result = Execute("$x = 6; $x *= 7", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(42));
            Assert.That(backing["$x"], Is.EqualTo(42));
        }
    }

    [Test]
    public void CoalesceAssign_Sets_When_Null_And_Returns_Value()
    {
        var result = Execute("$x = null; $x ??= 123; $x", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(123));
            Assert.That(backing["$x"], Is.EqualTo(123));
        }
    }

    [Test]
    public void CoalesceAssign_DoesNot_Override_When_NotNull()
    {
        var result = Execute("$x = 50; $x ??= 123; $x", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(50));
            Assert.That(backing["$x"], Is.EqualTo(50));
        }
    }

    [Test]
    public void LogicalAnd_ShortCircuit_Skips_Right_When_Left_False()
    {
        var result = Execute("$hit = 0; false && ($hit = 1); $hit", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(0));
            Assert.That(backing["$hit"], Is.EqualTo(0));
        }
    }

    [Test]
    public void LogicalOr_ShortCircuit_Skips_Right_When_Left_True()
    {
        var result = Execute("$hit = 0; true || ($hit = 1); $hit", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(0));
            Assert.That(backing["$hit"], Is.EqualTo(0));
        }
    }

    [Test]
    public void Coalesce_ShortCircuit_Skips_Fallback_When_Left_NotNull()
    {
        var result = Execute("$hit = 0; 1 ?? ($hit = 1); $hit", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(0));
            Assert.That(backing["$hit"], Is.EqualTo(0));
        }
    }

    [Test]
    public void Ternary_ShortCircuit_Skips_Unchosen_Branch()
    {
        var result = Execute("$hit = 0; true ? 1 : ($hit = 1); $hit", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(0));
            Assert.That(backing["$hit"], Is.EqualTo(0));
        }
    }

    [Test]
    public void PreIncrement_Returns_New_Value()
    {
        var result = Execute("$c = 10; ++$c", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(11));
            Assert.That(backing["$c"], Is.EqualTo(11));
        }
    }

    [Test]
    public void PostIncrement_Returns_Old_Value()
    {
        var result = Execute("$c = 10; $c++", out var backing, useDirectResources: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(10));
            Assert.That(backing["$c"], Is.EqualTo(11));
        }
    }

    [Test]
    public void Parameter_Expression_Returns_Parameter()
    {
        var p = new object();
        var result = Execute("$parameter", out var _, useDirectResources: true, parameter: p);

        Assert.That(result, Is.SameAs(p));
    }

    [Test]
    public void Self_Expression_Returns_Owner()
    {
        var owner = new object();
        var result = Execute("$self", out var _, useDirectResources: true, owner: owner);

        Assert.That(result, Is.SameAs(owner));
    }

    [Test]
    public void Assignment_Uses_SetResource_When_NotDirect_And_Key_Exists_In_Parent()
    {
        var (parent, parentBacking) = ResourcesMock.CreateResources();
        parentBacking["$A"] = 10;

        var result = Execute("$A = 123; $A", out var childBacking, useDirectResources: false, parent: parent);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(123));
            Assert.That(parentBacking["$A"], Is.EqualTo(123));
            Assert.That(childBacking.ContainsKey("$A"), Is.False);
        }
    }

    [Test]
    public void Assign_To_Parameter_Throws()
    {
        Assert.That(
            () => Execute("$parameter = 1", out var _, useDirectResources: true),
            Throws.InstanceOf<InvalidOperationException>());
    }
}
