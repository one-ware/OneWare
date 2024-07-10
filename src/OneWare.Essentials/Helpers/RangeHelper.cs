using Avalonia.Media;
using AvaloniaEdit.Document;
using OneWare.Essentials.EditorExtensions;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OneWare.Essentials.Helpers;

public static class RangeHelper
{
    public static TextModificationSegment GenerateTextModification(this Range range, TextDocument document, IBrush? foreground, IBrush? background = null, TextDecorationCollection? decorations = null)
    {
        var offset = document.GetStartAndEndOffset(range);
        return new TextModificationSegment(offset.startOffset, offset.endOffset)
        {
            Foreground = foreground,
            Background = background,
            Decorations = decorations
        };
    }
}