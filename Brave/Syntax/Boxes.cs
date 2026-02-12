using System;
using System.Collections.Generic;
using System.Text;

namespace Brave.Syntax;

internal static class Boxes
{
    public static readonly object BoxedTrue = true;
    public static readonly object BoxedFalse = false;

    // integer
    public static readonly object BoxedInt0 = 0;
    public static readonly object BoxedInt1 = 1;
    public static readonly object BoxedIntNeg1 = -1;

    // double
    public static readonly object BoxedDouble0 = 0.0;
    public static readonly object BoxedDouble1 = 1.0;
    public static readonly object BoxedDoubleNeg1 = -1.0;


    public static object Box(bool value) => value ? BoxedTrue : BoxedFalse;

    public static object Box(int value)
    {
        return value switch
        {
            0 => BoxedInt0,
            1 => BoxedInt1,
            -1 => BoxedIntNeg1,
            _ => value,
        };
    }

    public static object Box(uint value) => value;

    public static object Box(double value)
    {
        return value switch
        {
            0.0 => BoxedDouble0,
            1.0 => BoxedDouble1,
            -1.0 => BoxedDoubleNeg1,
            _ => value,
        };
    }

    public static object Box(string value) => value;

    public static object Box(long value) => value;

    public static object Box(ulong value) => value;

    public static object Box(float value) => value;

    public static object Box(decimal value) => value;

    public static object Box(object self) => self;
}
