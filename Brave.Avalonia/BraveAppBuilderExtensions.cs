using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Brave.Avalonia;

public static class BraveAppBuilderExtensions
{
    public static AppBuilder UseBrave(this AppBuilder builder)
    {
        BraveConstants.UnsetValue = AvaloniaProperty.UnsetValue;
        BraveConstants.FrameworkConverter = (value, targetType) =>
        {
            return DefaultValueConverter.Instance.Convert(value, targetType, null, CultureInfo.CurrentUICulture);
        };

        return builder;
    }
}
