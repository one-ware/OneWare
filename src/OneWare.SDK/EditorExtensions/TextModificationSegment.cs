using Avalonia.Media;
using AvaloniaEdit.Document;

namespace OneWare.SDK.EditorExtensions;

public class TextModificationSegment : TextSegment
{
    public TextModificationSegment(int startOffset, int endOffset)
    {
        StartOffset = startOffset < 0 ? 0 : startOffset;
        EndOffset = endOffset;
    }

    public IBrush? Brush { get; set; }
        
    public TextDecorationCollection? Decorations { get; set; }
}