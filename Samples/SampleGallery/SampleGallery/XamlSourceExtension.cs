using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using TextMateSharp.Grammars;

namespace SampleGallery;

public sealed partial class XamlSourceExtension
{
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public XamlSourceExtension()
    {
        Key = null!;
    }

    public XamlSourceExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key
    {
        get; set;
    }

    public string ProvideValue(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService(typeof(IUriContext)) is not IUriContext { BaseUri: var baseUri })
        {
            throw new InvalidOperationException("Unable to determine the base URI for the XAML source.");
        }

        var path = GetResourceName(baseUri);
        var executingAssembly = Assembly.GetExecutingAssembly();

        using var stream = executingAssembly.GetManifestResourceStream(path);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        var rawXaml = reader.ReadToEnd();

        var result = ExtractElement(rawXaml, Key);
        result = RemoveTextEditor(result);

        if(serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget { TargetObject: TextEditor textEditor })
        {
            var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            var language = registryOptions.GetLanguageByExtension(".axaml");
            var textMateInstallation = textEditor.InstallTextMate(registryOptions);
            textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(language.Id));
        }

        return result;
    }

    private static string ExtractElement(string xaml, string key)
    {
        var keyPattern = KeyAttributePattern(Regex.Escape(key));
        var keyMatch = keyPattern.Match(xaml);
        if (!keyMatch.Success)
        {
            return string.Empty;
        }

        // Walk backwards from the match to find the start of the opening tag '<'
        var tagStart = xaml.LastIndexOf('<', keyMatch.Index);
        if (tagStart < 0)
        {
            return string.Empty;
        }

        // Determine the indentation of the opening tag line
        var lineStart = xaml.LastIndexOf('\n', tagStart);
        var indent = xaml[(lineStart + 1)..tagStart];

        // Extract the element name from the opening tag
        var nameMatch = ElementNamePattern().Match(xaml, tagStart);
        if (!nameMatch.Success)
        {
            return string.Empty;
        }

        var elementName = nameMatch.Groups[1].Value;

        // Check for self-closing tag
        var openingTagEnd = xaml.IndexOf('>', keyMatch.Index);
        if (openingTagEnd < 0)
        {
            return string.Empty;
        }

        if (xaml[openingTagEnd - 1] == '/')
        {
            return Dedent(xaml[tagStart..(openingTagEnd + 1)], indent);
        }

        // Find the matching closing tag, accounting for nesting
        var depth = 1;
        var pos = openingTagEnd + 1;
        var openTag = $"<{elementName}";
        var closeTag = $"</{elementName}";

        while (depth > 0 && pos < xaml.Length)
        {
            var nextOpen = xaml.IndexOf(openTag, pos, StringComparison.Ordinal);
            var nextClose = xaml.IndexOf(closeTag, pos, StringComparison.Ordinal);

            if (nextClose < 0)
            {
                return string.Empty;
            }

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                // Check if this open tag is self-closing
                var selfCloseEnd = xaml.IndexOf('>', nextOpen);
                if (selfCloseEnd >= 0 && xaml[selfCloseEnd - 1] == '/')
                {
                    pos = selfCloseEnd + 1;
                }
                else
                {
                    depth++;
                    pos = nextOpen + openTag.Length;
                }
            }
            else
            {
                depth--;
                if (depth == 0)
                {
                    var closeEnd = xaml.IndexOf('>', nextClose);
                    if (closeEnd < 0)
                    {
                        return string.Empty;
                    }

                    return Dedent(xaml[tagStart..(closeEnd + 1)], indent);
                }

                pos = nextClose + closeTag.Length;
            }
        }

        return string.Empty;
    }

    private static string Dedent(string text, string indent)
    {
        if (string.IsNullOrEmpty(indent))
        {
            return text;
        }

        return text.Replace("\n" + indent, "\n");
    }

    private static string RemoveTextEditor(string text)
    {
        var idx = text.IndexOf("TextEditor", StringComparison.Ordinal);
        if (idx < 0)
        {
            return text;
        }

        // Walk back to '<'
        var start = text.LastIndexOf('<', idx);
        if (start < 0)
        {
            return text;
        }

        // Walk back further to include leading whitespace on the same line
        while (start > 0 && text[start - 1] is '\t' or ' ')
        {
            start--;
        }

        // Find '/>' after TextEditor
        var end = text.IndexOf("/>", idx, StringComparison.Ordinal);
        if (end < 0)
        {
            return text;
        }

        end += 2; // past '/>'

        // Skip trailing newlines only (preserve indentation of next line)
        while (end < text.Length && text[end] is '\r' or '\n')
        {
            end++;
        }

        return string.Concat(text.AsSpan(0, start), text.AsSpan(end));
    }

    [GeneratedRegex(@"<([a-zA-Z_][\w:.]*)\s")]
    private static partial Regex ElementNamePattern();

    private static Regex KeyAttributePattern(string escapedKey) =>
        new(@"x:Key\s*=\s*""" + escapedKey + @"""");

    private static string GetResourceName(Uri uri)
    {
        var path = uri.AbsolutePath.TrimStart('/');
        return $"{uri.Host}.{path.Replace('/', '.')}";
    }
}
