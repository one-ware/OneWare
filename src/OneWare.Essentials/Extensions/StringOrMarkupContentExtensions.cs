using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace OneWare.Essentials.Extensions;

public static class StringOrMarkupContentExtensions
{
    /// <summary>
    ///     Reads the text of a value that a language server may send either as a plain string or as
    ///     markup, for places that can only display plain text.
    /// </summary>
    public static string? GetPlainText(this StringOrMarkupContent? content)
    {
        if (content == null) return null;
        if (content.HasString) return content.String;
        if (content.HasMarkupContent) return content.MarkupContent?.Value;
        return null;
    }

    /// <summary>
    ///     Reads the text of a value that a language server may send either as a plain string or as
    ///     markup and returns it as markdown, ready to be shown in a markdown viewer.
    /// </summary>
    public static string? GetMarkdown(this StringOrMarkupContent? content)
    {
        if (content == null) return null;

        var markdown = content switch
        {
            { HasMarkupContent: true, MarkupContent: { } markup } => markup.Kind == MarkupKind.Markdown
                ? markup.Value.Trim()
                : EscapeMarkdown(markup.Value).Trim(),
            { HasString: true } => EscapeMarkdown(content.String).Trim(),
            _ => null
        };

        return string.IsNullOrWhiteSpace(markdown) ? null : markdown;
    }

    /// <summary>
    ///     Turns plain text into markdown that renders verbatim. Line breaks are kept and the
    ///     characters that would otherwise be read as emphasis are escaped. Surrounding whitespace is
    ///     left alone so that fragments of a larger text can be escaped one by one.
    /// </summary>
    public static string EscapeMarkdown(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        var builder = new StringBuilder(plainText.Length + 16);

        foreach (var character in plainText.ReplaceLineEndings("\n"))
        {
            //Escaping anything else would leave the backslash visible, the renderer only knows these
            if (character is '\\' or '*' or '_' or '~') builder.Append('\\');
            //Two trailing spaces make the renderer keep the line break instead of joining the lines
            if (character is '\n') builder.Append("  ");
            builder.Append(character);
        }

        return builder.ToString();
    }
}
