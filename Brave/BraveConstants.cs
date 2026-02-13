using System;
using System.Collections.Generic;
using System.Text;

namespace Brave;

// TODO: Replace with IoC container or something like that
internal static class BraveConstants
{
    public static object UnsetValue = new();

    public static Func<object?, Type, object?>? FrameworkConverter = null;

    public static Func<Exception, object>? BindingNotificationFactory = null; 
}
