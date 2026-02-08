using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Brave.Commands;
using Brave.Compile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Input;

namespace Brave.Avalonia;

public sealed class ExecuteExtension
{
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public ExecuteExtension()
    {
        Expression = null!;
    }

    public ExecuteExtension(string expression)
    {
        Expression = expression;
    }

    [ConstructorArgument("expression")]
    public string Expression
    {
        get; set;
    }

    public ICommand? ProvideValue(IServiceProvider serviceProvider)
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

        var instructions = Compiler.Compile(Expression, useDirectSetResource: false);

        return new CommandExecutor(resources, instructions);
    }
}
