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
}
