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
}
