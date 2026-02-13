using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.XamlIl.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Brave.Avalonia;

internal sealed class MetaInfoProvider : IMetaInfoProvider
{
    private readonly IServiceProvider _serviceProvider;

    public MetaInfoProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        var provideValueTarget = _serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        var rootObjectProvider = _serviceProvider.GetService(typeof(IRootObjectProvider)) as IRootObjectProvider;

        RootObject = rootObjectProvider?.RootObject;
        IntermediateRootObject = rootObjectProvider?.IntermediateRootObject;
        CurrentObject = provideValueTarget?.TargetObject;

        RootResources = RootObject is StyledElement rootStyled ? new AvaloniaResources(rootStyled) : null;
        IntermediateRootResources = IntermediateRootObject is StyledElement intermediateRootStyled ? new AvaloniaResources(intermediateRootStyled) : null;

        var xmlNamespaceInfoProvider = _serviceProvider.GetService(typeof(IAvaloniaXamlIlXmlNamespaceInfoProvider)) ?? throw new ArgumentException("Cannot provide xaml namespace info", nameof(serviceProvider));

        XamlNamespaceInfoProvider = new AdapterXamlNamespaceInfoProvider((IAvaloniaXamlIlXmlNamespaceInfoProvider)xmlNamespaceInfoProvider);
    }

    public object? RootObject
    {
        get;
    }

    public object? IntermediateRootObject
    {
        get;
    }

    public object? CurrentObject
    {
        get;
    }

    public IAbstractResources? RootResources
    {
        get;
    }

    public IAbstractResources? IntermediateRootResources
    {
        get;
    }

    public IXamlNamespaceInfoProvider XamlNamespaceInfoProvider
    {
        get;
    }

    private sealed class AdapterXamlNamespaceInfoProvider : IXamlNamespaceInfoProvider
    {
        private readonly IAvaloniaXamlIlXmlNamespaceInfoProvider _xmlNamespaceInfoProvider;
        private readonly Dictionary<string, ImmutableArray<XamlNamespaceInfo>> _xamlNamespaces = [];

        public AdapterXamlNamespaceInfoProvider(IAvaloniaXamlIlXmlNamespaceInfoProvider xmlNamespaceInfoProvider)
        {
            _xmlNamespaceInfoProvider = xmlNamespaceInfoProvider;


            foreach (var item in xmlNamespaceInfoProvider.XmlNamespaces)
            {
                var array = item.Value.Select(x => new XamlNamespaceInfo(x.ClrNamespace, x.ClrAssemblyName)).ToImmutableArray();
                _xamlNamespaces[item.Key] = array;
            }
        }

        public IReadOnlyDictionary<string, ImmutableArray<XamlNamespaceInfo>> XamlNamespaces => _xamlNamespaces;
    }
}
