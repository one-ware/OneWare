using Avalonia.Media;
using AvaloniaEdit.Document;
using OneWare.SDK.EditorExtensions;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OneWare.SDK.Helpers;

public static class RangeHelper
{
    public static TextModificationSegment GenerateTextModification(this Range range, TextDocument document, IBrush brush)
    {
        var offset = document.GetStartAndEndOffset(range);
        return new TextModificationSegment(offset.startOffset, offset.endOffset)
        {
            Brush = brush
        };
    }
}