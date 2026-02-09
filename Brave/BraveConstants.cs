using System;
using System.Collections.Generic;
using System.Text;

namespace Brave;

internal static class BraveConstants
{
    public static object UnsetValue = new();

    public static Func<object?, Type, object?>? FrameworkConverter = null;
}
