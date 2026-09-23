using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.LanguageService;

/// <summary>
///     Turns what a language server reports about a symbol into the markdown that hover tips, completion
///     tips and the overload insight display.
/// </summary>
public static class LanguageServiceMarkdown
{
    /// <summary>
    ///     Separates the parts of a hover tip that come from different sources.
    /// </summary>
    public const string SectionSeparator = "\n\n---\n\n";

    /// <summary>
    ///     Renders the contents of a hover response. Markdown is passed through, everything else is escaped
    ///     so that it shows up the way the server wrote it.
    /// </summary>
    public static string? BuildHoverText(Hover hover)
    {
        if (hover.Contents.HasMarkupContent)
        {
            var markup = hover.Contents.MarkupContent;
            if (markup == null) return null;

            var value = markup.Kind == MarkupKind.Markdown
                ? markup.Value.Trim()
                : StringOrMarkupContentExtensions.EscapeMarkdown(markup.Value).Trim();

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        if (hover.Contents is not { HasMarkedStrings: true, MarkedStrings: not null }) return null;

        var segments = hover.Contents.MarkedStrings
            .Select(marked => string.IsNullOrWhiteSpace(marked.Language)
                ? StringOrMarkupContentExtensions.EscapeMarkdown(marked.Value).Trim()
                : $"```{marked.Language}\n{marked.Value.Trim()}\n```")
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        return segments.Count > 0 ? string.Join("\n\n", segments) : null;
    }

    /// <summary>
    ///     Renders the diagnostics under the cursor. Every diagnostic gets its severity and, if the server
    ///     reported them, the source and the code it belongs to.
    /// </summary>
    public static string? FormatDiagnostics(IReadOnlyList<ErrorListItem> errors)
    {
        if (errors.Count == 0) return null;

        var formatted = errors.Select(error =>
        {
            var description = StringOrMarkupContentExtensions.EscapeMarkdown(error.Description).Trim();
            return $"**{error.Type}**{FormatDiagnosticOrigin(error)}  \n{description}";
        });

        return string.Join("\n\n", formatted);
    }

    private static string FormatDiagnosticOrigin(ErrorListItem error)
    {
        var source = string.IsNullOrWhiteSpace(error.Source) ? null : error.Source;
        var code = string.IsNullOrWhiteSpace(error.Code) ? null : error.Code;

        return (source, code) switch
        {
            (not null, not null) => $" `{source} {code}`",
            (not null, null) => $" `{source}`",
            (null, not null) => $" `{code}`",
            _ => string.Empty
        };
    }

    /// <summary>
    ///     Builds the markdown shown next to the completion list. The type information the server sends as
    ///     detail goes into a code block, followed by the documentation of the symbol.
    /// </summary>
    public static string? BuildCompletionDescription(CompletionItem completionItem, string languageId)
    {
        var sections = new List<string>();

        if (IsDeprecated(completionItem)) sections.Add("**Deprecated**");

        var hasDetail = !string.IsNullOrWhiteSpace(completionItem.Detail);
        var signature = hasDetail
            ? completionItem.Detail
            : completionItem.Label + completionItem.LabelDetails?.Detail;

        if (!string.IsNullOrWhiteSpace(signature))
            sections.Add($"```{languageId}\n{signature.Trim()}\n```");

        var origin = completionItem.LabelDetails?.Description;
        if (!string.IsNullOrWhiteSpace(origin))
            sections.Add(StringOrMarkupContentExtensions.EscapeMarkdown(origin).Trim());

        var documentation = completionItem.Documentation.GetMarkdown();
        if (!string.IsNullOrWhiteSpace(documentation)) sections.Add(documentation);

        //Repeating the label the list already shows is not worth opening a tip for
        if (sections.Count == 0 || (sections.Count == 1 && !hasDetail)) return null;

        return string.Join("\n\n", sections);
    }

    public static bool IsDeprecated(CompletionItem completionItem)
    {
        return completionItem.Deprecated || (completionItem.Tags?.Contains(CompletionItemTag.Deprecated) ?? false);
    }

    /// <summary>
    ///     Renders a signature with the parameter the caret is on in bold. The overload insight already uses
    ///     the editor font, so no code fence is needed and the emphasis survives.
    /// </summary>
    public static string FormatSignatureLabel(SignatureInformation signature, ParameterInformation? activeParameter)
    {
        var label = signature.Label ?? string.Empty;

        if (GetParameterRange(signature, activeParameter) is not { } range)
            return StringOrMarkupContentExtensions.EscapeMarkdown(label);

        var prefix = StringOrMarkupContentExtensions.EscapeMarkdown(label[..range.start]);
        var active = StringOrMarkupContentExtensions.EscapeMarkdown(label[range.start..range.end]);
        var suffix = StringOrMarkupContentExtensions.EscapeMarkdown(label[range.end..]);

        return string.IsNullOrEmpty(active) ? prefix + suffix : $"{prefix}**{active}**{suffix}";
    }

    /// <summary>
    ///     Renders the documentation of a signature, led by the parameter the caret is on.
    /// </summary>
    public static string? FormatSignatureDocumentation(SignatureInformation signature,
        ParameterInformation? activeParameter)
    {
        var sections = new List<string>();

        var parameterName = GetParameterLabelText(signature, activeParameter);
        var parameterDocumentation = activeParameter?.Documentation.GetMarkdown();

        if (!string.IsNullOrWhiteSpace(parameterName))
        {
            var name = StringOrMarkupContentExtensions.EscapeMarkdown(parameterName);
            sections.Add(string.IsNullOrWhiteSpace(parameterDocumentation)
                ? $"**{name}**"
                : $"**{name}** — {parameterDocumentation}");
        }
        else if (!string.IsNullOrWhiteSpace(parameterDocumentation))
        {
            sections.Add(parameterDocumentation);
        }

        var documentation = signature.Documentation.GetMarkdown();
        if (!string.IsNullOrWhiteSpace(documentation)) sections.Add(documentation);

        return sections.Count > 0 ? string.Join("\n\n", sections) : null;
    }

    /// <summary>
    ///     Resolves the parameter the caret is on. Servers report it either per signature or once for the
    ///     whole request, so both are taken into account.
    /// </summary>
    public static ParameterInformation? GetActiveParameter(SignatureHelp signatureHelp,
        SignatureInformation signature)
    {
        var parameters = signature.Parameters?.ToList();
        if (parameters is not { Count: > 0 }) return null;

        var index = signature.ActiveParameter ?? signatureHelp.ActiveParameter;
        if (index is not { } value || value < 0 || value >= parameters.Count) return null;

        return parameters[value];
    }

    /// <summary>
    ///     Locates a parameter inside the label of its signature. A server either sends the parameter text
    ///     itself or, when it announced offset support, the range it covers in the signature label.
    /// </summary>
    public static (int start, int end)? GetParameterRange(SignatureInformation signature,
        ParameterInformation? parameter)
    {
        if (parameter == null) return null;

        var label = signature.Label ?? string.Empty;

        if (parameter.Label.IsRange)
        {
            var (start, end) = parameter.Label.Range;
            return start >= 0 && end <= label.Length && start < end ? (start, end) : null;
        }

        if (!parameter.Label.IsLabel || string.IsNullOrEmpty(parameter.Label.Label)) return null;

        var index = label.IndexOf(parameter.Label.Label, StringComparison.Ordinal);
        return index >= 0 ? (index, index + parameter.Label.Label.Length) : null;
    }

    /// <summary>
    ///     Reads the text of a parameter, resolving offsets against the label of its signature.
    /// </summary>
    public static string? GetParameterLabelText(SignatureInformation signature, ParameterInformation? parameter)
    {
        if (parameter == null) return null;

        if (GetParameterRange(signature, parameter) is { } range)
            return (signature.Label ?? string.Empty)[range.start..range.end];

        return parameter.Label.IsLabel ? parameter.Label.Label : null;
    }
}
