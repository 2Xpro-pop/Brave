using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.XamlIl.Runtime;
using Brave.Commands;
using Brave.Compile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.WebSockets;
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
        var styled = GetTargetObject(serviceProvider);

        if (Expression is null)
        {
            throw new InvalidOperationException("Expression cannot be null.");
        }

        var metaInfoProvider = new MetaInfoProvider(serviceProvider);

        var resources = new AvaloniaResources(styled);

        var instructions = Compiler.Compile(Expression, useDirectSetResource: false);

        return new CommandExecutor(resources, instructions, metaInfoProvider);
    }

    private static StyledElement GetTargetObject(IServiceProvider serviceProvider)
    {

        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget provideValueTarget)
        {
            if (provideValueTarget.TargetObject is StyledElement styled)
            {
                return styled;
            }
        }

        if(serviceProvider.GetService(typeof(IRootObjectProvider)) is IRootObjectProvider rootObjectProvider)
        {
            if (rootObjectProvider.RootObject is StyledElement styled)
            {
                return styled;
            }

            if(rootObjectProvider.IntermediateRootObject is StyledElement styledIntermediate)
            {
                return styledIntermediate;
            }
        }

        throw new InvalidOperationException("Target or Root object is not a StyledElement.");
    }
}
