using Avalonia;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brave.Avalonia;

internal static class TargetObjectFinder
{
    public static StyledElement Find(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget provideValueTarget)
        {
            if (provideValueTarget.TargetObject is StyledElement styled)
            {
                return styled;
            }
        }

        if (serviceProvider.GetService(typeof(IRootObjectProvider)) is IRootObjectProvider rootObjectProvider)
        {
            if (rootObjectProvider.RootObject is StyledElement styled)
            {
                return styled;
            }

            if (rootObjectProvider.IntermediateRootObject is StyledElement styledIntermediate)
            {
                return styledIntermediate;
            }
        }

        throw new InvalidOperationException("Target or Root object is not a StyledElement.");
    }
}
