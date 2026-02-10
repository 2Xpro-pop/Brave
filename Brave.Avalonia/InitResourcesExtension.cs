using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.XamlIl.Runtime;
using Brave.Commands;
using Brave.Compile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Brave.Avalonia;

public sealed class InitResourcesExtension
{
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public InitResourcesExtension()
    {
        Expression = null!;
    }

    public InitResourcesExtension(string expression)
    {
        Expression = expression;
    }

    [ConstructorArgument("expression")]
    public string Expression
    {
        get; set;
    }

    public IResourceDictionary ProvideValue(IServiceProvider serviceProvider)
    {
        var styled = TargetObjectFinder.Find(serviceProvider);

        if (Expression is null)
        {
            throw new InvalidOperationException("Expression cannot be null.");
        }

        var resources = new AvaloniaResources(styled);
        var metaInfoProvider = new MetaInfoProvider(serviceProvider);

        var instructions = Compiler.Compile(Expression, useDirectSetResource: true);

        Interpretator.Execute(resources, parameter: null, metaInfoProvider, instructions);

        return styled.Resources;
    }
}
