using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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
        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not IProvideValueTarget provideValueTarget)
        {
            throw new InvalidOperationException("IProvideValueTarget service is not available.");
        }

        if (provideValueTarget.TargetObject is not StyledElement styled)
        {
            throw new InvalidOperationException("Target object is not a StyledElement.");
        }

        if (Expression is null)
        {
            throw new InvalidOperationException("Expression cannot be null.");
        }

        var resources = new AvaloniaResources(styled);

        var instructions = Compiler.Compile(Expression, useDirectSetResource: true);

        Interpretator.Execute(resources, parameter: null, instructions);

        return styled.Resources;
    }
}
