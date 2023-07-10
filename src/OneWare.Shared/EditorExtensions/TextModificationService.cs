using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ImTools;
using OneWare.Shared.Enums;
using OneWare.Shared.Models;

namespace OneWare.Shared.EditorExtensions;

public class TextModificationService : DocumentColorizingTransformer
{
    private readonly TextSegmentCollection<TextModificationSegment> _modificationSegments;

    private readonly TextView _textView;
    
    public TextModificationService(TextView textView)
    {
        _textView = textView;
        _modificationSegments = new TextSegmentCollection<TextModificationSegment>(textView.Document);
    }

    public void SetDiagnostics(IEnumerable<ErrorListItemModel>? diagnostics)
    {
        var copy = new TextModificationSegment[_modificationSegments.Count];
        _modificationSegments.CopyTo(copy,0);
        foreach(var s in copy)
        {
            _modificationSegments.Remove(s);
            _textView.Redraw(s);
        }

        if (diagnostics == null) return;
        foreach (var diag in diagnostics)
        {
            IBrush markerColor = diag.Type switch
            {
                ErrorType.Error => Brushes.Red,
                ErrorType.Warning => Brushes.DarkGray,
                _ => Brushes.DarkGray
            };

            var offset = diag.GetOffset(_textView.Document);

            var sOff = offset.startOffset;
            var eOff = offset.endOffset;
            var overlap = _modificationSegments.FindOverlappingSegments(sOff, eOff);
            if (overlap.Any())
            {
                var f = overlap.First();
                if (sOff >= f.StartOffset)
                {
                    if(eOff <= f.EndOffset) return; //Completely overlapped
                    if (eOff > f.EndOffset + 1) sOff = f.EndOffset + 1;
                }
                if (sOff < f.StartOffset)
                {
                    if (eOff > f.EndOffset - 1) eOff = f.EndOffset - 1;
                }
            }

            _modificationSegments.Add(new TextModificationSegment(sOff, eOff){Brush = markerColor});
            _textView.Redraw(sOff, eOff-sOff);
        }
    }
    
    protected override void ColorizeLine(DocumentLine line)
    {
        if (!line.IsDeleted)
        {
            var overlaps = _modificationSegments.FindOverlappingSegments(line.Offset, line.Length);

            foreach (var overlap in overlaps)
            {
                if (overlap.EndOffset > line.EndOffset) overlap.EndOffset = line.EndOffset;
                ChangeLinePart(overlap.StartOffset, overlap.EndOffset, (x) => ApplyChanges(x, overlap.Brush, overlap.Decorations));
            }
        }
    }

    private void ApplyChanges(VisualLineElement element, IBrush? color, TextDecorationCollection? decorations)
    {
        // This is where you do anything with the line
        if (color != null) element.TextRunProperties.SetForegroundBrush(color);
        if (decorations != null) element.TextRunProperties.SetTextDecorations(decorations);
    }
    
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
}