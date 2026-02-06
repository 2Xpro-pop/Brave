using Brave.Collections;
using Brave.Pools;
using Brave.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brave.Commands;

// Stack shoud be small
internal sealed class RuntimeStack: IDisposable
{
    public static class Indexes
    {
        public static object Last = new();
        public static object SecondLast = new();
        public static object ThirdLast = new();
    }

    const int InitialCapacity = 8;
    const int MaxCapacity = 64;

    private static readonly ObjectPool<ArrayElement<object?>[]> _pool = new(() => new ArrayElement<object?>[InitialCapacity]);

    private ArrayElement<object?>[] _stack;
    private int _count;

    public RuntimeStack()
    {
        _stack = _pool.Allocate();
        _count = 0;
    }

    public int Count => _count;
    public int Capacity => _stack.Length;

    public void Push(object? value)
    {
        if (_count == _stack.Length)
        {
            Grow();
        }

        _stack[_count++].Value = value;
    }

    public void Push(int value)
    {
        switch (value)
        {
            case 0:
                Push(Boxes.BoxedInt0);
                break;

            case 1:
                Push(Boxes.BoxedInt1);
                break;

            case -1:
                Push(Boxes.BoxedIntNeg1);
                break;

            default:
                Push((object)value);
                break;
        }
    }

    public void Push(double value)
    {
        switch (value)
        {
            case 0:
                Push(Boxes.BoxedDouble0);
                break;

            case 1:
                Push(Boxes.BoxedDouble1);
                break;

            case -1:
                Push(Boxes.BoxedDoubleNeg1);
                break;

            default:
                Push((object)value);
                break;
        }
    }

    public void Push(bool value)
    {
        Push(value ? Boxes.BoxedTrue : Boxes.BoxedFalse);
    }

    public object? Pop()
    {
        if (_count == 0)
        {
            ThrowEmpty();
        }

        var index = --_count;
        var value = _stack[index].Value;

        // Clear reference to avoid retaining objects (especially when returning to pool)
        _stack[index].Value = null!;

        return value;
    }

    public object? PopOrReturn(object? value)
    {
        if (ReferenceEquals(Indexes.Last, value))
        {
            return Pop();
        }

        if(ReferenceEquals(Indexes.SecondLast, value))
        {
            var last = Pop();
            var secondLast = Pop();
            Push(last!);
            return secondLast;
        }

        if(ReferenceEquals(Indexes.ThirdLast, value))
        {
            var last = Pop();
            var secondLast = Pop();
            var thirdLast = Pop();
            Push(secondLast!);
            Push(last!);
            return thirdLast;
        }

        return value;
    }

    public bool TryPop(out object? value)
    {
        if (_count == 0)
        {
            value = null;
            return false;
        }

        value = Pop();
        return true;
    }

    public object? Peek()
    {
        if (_count == 0)
        {
            ThrowEmpty();
        }

        return _stack[_count - 1].Value;
    }

    public bool TryPeek(out object? value)
    {
        if (_count == 0)
        {
            value = null;
            return false;
        }

        value = _stack[_count - 1].Value;
        return true;
    }

    public void Clear()
    {
        for (var index = 0; index < _count; index++)
        {
            _stack[index].Value = null!;
        }

        _count = 0;
    }

    private void Grow()
    {
        var newCapacity = _stack.Length * 2;
        if (newCapacity > MaxCapacity)
        {
            throw new InvalidOperationException("Runtime stack exceeded maximum capacity.");
        }

        var oldStack = _stack;
        var oldCount = _count;

        var newStack = new ArrayElement<object?>[newCapacity];
        Array.Copy(oldStack, newStack, oldCount);

        // Clear references in old stack before pooling/discarding
        for (var index = 0; index < oldCount; index++)
        {
            oldStack[index].Value = null;
        }

        if (oldStack.Length == InitialCapacity)
        {
            _pool.Free(oldStack);
        }

        _stack = newStack;
        _count = oldCount;
    }

    public void Dispose()
    {
        Clear();

        var stackToReturn = _stack;
        _stack = [];
        _count = 0;

        if (stackToReturn.Length == InitialCapacity)
        {
            _pool.Free(stackToReturn);
        }

        GC.SuppressFinalize(this);
    }

    ~RuntimeStack()
    {
        Dispose();
    }

    private static void ThrowEmpty()
    {
        throw new InvalidOperationException("Runtime stack is empty.");
    }
}
