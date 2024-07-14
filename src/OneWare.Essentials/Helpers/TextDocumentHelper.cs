using AvaloniaEdit.Document;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OneWare.Essentials.Helpers;

public static class TextDocumentHelper
{
    /// <summary>
    ///     Returns start and end offsets. If no end offset is specified, the whole line length is used
    /// </summary>
    public static (int startOffset, int endOffset) GetStartAndEndOffset(this TextDocument document, int startLine,
        int? startColumn, int? endLine, int? endColumn)
    {
        if (startLine <= 0) return (0, 0);
        if (startLine > document.LineCount) return (document.TextLength, document.TextLength);

        var startOffset = document.GetOffset(startLine, startColumn ?? 0);

        if (endLine != null && endColumn != null)
        {
            if (endLine > document.LineCount) endLine = document.LineCount;
            var endOffset = document.GetOffset(endLine.Value, endColumn.Value);
            return (startOffset, endOffset);
        }

        var lineLength = document.GetLineByNumber(startLine).Length;
        return (startOffset, startOffset + lineLength);
    }

    public static (int startOffset, int endOffset) GetStartAndEndOffset(this TextDocument document, Range range)
    {
        return document.GetStartAndEndOffset(range.Start.Line + 1, range.Start.Character + 1, range.End.Line + 1,
            range.End.Character + 1);
    }
}