using System;
using System.Linq;
using AvaloniaEdit.Document;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Json.Formatting;

public class JsonFormatter : IFormattingStrategy
{
    private readonly ILogger _logger;

    public JsonFormatter(ILogger logger)
    {
        _logger = logger;
    }

    public void Format(TextDocument document)
    {
        try
        {
            document.Text = FormatJson(document.Text);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    public static string FormatJson(string json, string indent = "  ")
    {
        int indentation = 0;
        int quoteCount = 0;
        int escapeCount = 0;

        var result =
            from ch in json ?? string.Empty
            let escaped = (ch == '\\' ? escapeCount++ : escapeCount > 0 ? escapeCount-- : escapeCount) > 0
            let quotes = ch == '"' && !escaped ? quoteCount++ : quoteCount
            let unquoted = quotes % 2 == 0
            let colon = ch == ':' && unquoted ? ": " : null
            let nospace = char.IsWhiteSpace(ch) && unquoted ? string.Empty : null
            let lineBreak = ch == ',' && unquoted
                ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(indent, indentation))
                : null
            let openChar = (ch == '{' || ch == '[') && unquoted
                ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(indent, ++indentation))
                : ch.ToString()
            let closeChar = (ch == '}' || ch == ']') && unquoted
                ? Environment.NewLine + string.Concat(Enumerable.Repeat(indent, --indentation)) + ch
                : ch.ToString()
            select colon ?? nospace ?? lineBreak ?? (
                openChar.Length > 1 ? openChar : closeChar
            );

        return string.Concat(result);
    }
}
