﻿using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace OneWare.Essentials.EditorExtensions;

public abstract class GenericLineTransformer : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        TransformLine(line, CurrentContext);
    }

    protected abstract void TransformLine(DocumentLine line, ITextRunConstructionContext context);

    public void SetTextOpacity(DocumentLine line, int startIndex, int length, double opacity)
    {
        if (startIndex >= 0 && length > 0)
        {
            if (line.Offset + startIndex + length > line.EndOffset)
                length = line.EndOffset - startIndex - line.Offset - startIndex;

            var startOffset = line.Offset + startIndex;
            var endoffset = line.Offset + startIndex + length;

            if (startOffset < CurrentContext.Document.TextLength && endoffset < CurrentContext.Document.TextLength)
                ChangeLinePart(startOffset, endoffset, e =>
                {
                    if (e.TextRunProperties.ForegroundBrush is SolidColorBrush solidBrush)
                        e.TextRunProperties.SetForegroundBrush(new SolidColorBrush(solidBrush.Color, opacity));
                });
        }
        else
        {
            ChangeLinePart(line.Offset, line.EndOffset, e =>
            {
                if (e.TextRunProperties.ForegroundBrush is SolidColorBrush solidBrush)
                    e.TextRunProperties.SetForegroundBrush(new SolidColorBrush(solidBrush.Color, opacity));
            });
        }
    }

    public void SetTextStyle(DocumentLine line, int startIndex, int length, IBrush foreground)
    {
        if (startIndex >= 0 && length > 0)
        {
            if (line.Offset + startIndex + length > line.EndOffset)
                length = line.EndOffset - startIndex - line.Offset - startIndex;

            var startOffset = line.Offset + startIndex;
            var endoffset = line.Offset + startIndex + length;

            if (startOffset < CurrentContext.Document.TextLength && endoffset < CurrentContext.Document.TextLength)
                ChangeLinePart(startOffset, endoffset, e => { e.TextRunProperties.SetForegroundBrush(foreground); });
        }
        else
        {
            ChangeLinePart(line.Offset, line.EndOffset, e => { e.TextRunProperties.SetForegroundBrush(foreground); });
        }
    }
}