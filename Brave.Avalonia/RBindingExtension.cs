using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.XamlIl.Runtime;
using Brave.Commands;
using Brave.Compile;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace Brave.Avalonia;

public sealed class RBindingExtension
{
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public RBindingExtension()
    {
        Expression = null!;
    }

    public RBindingExtension(string expression)
    {
        Expression = expression;
    }

    [ConstructorArgument("expression")]
    public string Expression
    {
        get; set;
    }

    public BindingMode BindingMode
    {
        get; set;
    } = BindingMode.Default;


    public object? ProvideValue(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not IProvideValueTarget provideValueTarget)
        {
            throw new NotSupportedException("IProvideValueTarget is required");
        }

        var target = provideValueTarget.TargetProperty;
        var owner = provideValueTarget.TargetObject;

        var binding = GetBindingMode(target, owner);
        var instructions = Compiler.Compile(Expression);

        if (serviceProvider.GetService(typeof(IAvaloniaXamlIlParentStackProvider)) is not IAvaloniaXamlIlParentStackProvider parentStackProvider)
        {
            throw new NotSupportedException("IAvaloniaXamlIlParentStackProvider is required");
        }

        var resources = AvaloniaResources.GetFirstResources(parentStackProvider, owner);

        if (binding == BindingMode.TwoWay)
        {
            return CreateTwoWayBinding(target, owner, instructions, resources);
        }

        if (binding is BindingMode.OneWay or BindingMode.Default)
        {
            return CreateOneWayBinding((IPropertyInfo)target, instructions, resources);
        }

        if (binding == BindingMode.OneTime)
        {
            return CreateOneTimeBinding(instructions, resources);
        }

        if (binding == BindingMode.OneWayToSource)
        {
            return CreateOneWayToSource(target, owner, instructions, resources);
        }

        return AvaloniaProperty.UnsetValue;
    }

    private static IBinding? CreateTwoWayBinding(object targetProperty, object owner, ImmutableArray<CommandInstruction> instructions, IAbstractResources resources)
    {
        if (instructions.Length > 1)
        {
            throw new NotSupportedException("TwoWay binding only supports a single instruction. Only Identifier allowed");
        }

        if (instructions[0] is not { OpCode: CommandOpCode.GetResource })
        {
            throw new NotSupportedException("TwoWay binding only supports a single instruction. Only Identifier allowed");
        }

        var key = instructions[0].Arguments[0];
        var source = GetSource(targetProperty, owner);


        return new TwoWayRBinding(key, source, resources)
        {
            TargetConverter = (obj) => DefaultValueConverter.Instance.Convert(obj, ((IPropertyInfo)targetProperty).PropertyType, null, CultureInfo.CurrentUICulture)
        }.ToBinding();
    }

    private static IBinding? CreateOneWayBinding(IPropertyInfo propertyInfo, ImmutableArray<CommandInstruction> instructions, IAbstractResources resources)
    {
        var observableExpression = new ObservableExpression(resources, instructions)
        {
            TargetConverter = (obj) => DefaultValueConverter.Instance.Convert(obj, propertyInfo.PropertyType, null, CultureInfo.CurrentUICulture)
        };

        return observableExpression.ToBinding();
    }

    private static object? CreateOneTimeBinding(ImmutableArray<CommandInstruction> instructions, IAbstractResources resources)
    {
        return Interpretator.Execute(resources, null, instructions);
    }

    private static object CreateOneWayToSource(object targetProperty, object owner, ImmutableArray<CommandInstruction> instructions, IAbstractResources resources)
    {
        var source = GetSource(targetProperty, owner);

        source.Subscribe(new OneWayToSourceObserver(instructions, resources));

        return AvaloniaProperty.UnsetValue;
    }

    private BindingMode GetBindingMode(object targetProperty, object owner)
    {
        if (targetProperty is not AvaloniaProperty avaloniaProperty)
        {
            return BindingMode.OneWay;
        }

        if (BindingMode != BindingMode.Default)
        {
            return BindingMode;
        }

        return avaloniaProperty.GetMetadata(owner.GetType()).DefaultBindingMode;
    }

    private static IObservable<object?> GetSource(object targetProperty, object owner)
    {
        if (owner is AvaloniaObject avaloniaObject && targetProperty is AvaloniaProperty avaloniaProperty)
        {
            return avaloniaObject.GetObservable(avaloniaProperty);
        }
        else if (targetProperty is IPropertyInfo propertyInfo)
        {
            return new NpcObservable(new InstancedProperty(owner, propertyInfo));
        }
        throw new NotSupportedException("Unsupported target property type for TwoWay binding");
    }

    private sealed class OneWayToSourceObserver : IObserver<object?>
    {
        private readonly ImmutableArray<CommandInstruction> _instructions;
        private readonly IAbstractResources _resources;

        public OneWayToSourceObserver(ImmutableArray<CommandInstruction> instructions, IAbstractResources resources)
        {
            _instructions = instructions;
            _resources = resources;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(object? parameter)
        {
            Interpretator.Execute(_resources, parameter, _instructions);
        }
    }
}
