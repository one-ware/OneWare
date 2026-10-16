using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace OneWare.Essentials.EditorExtensions;

public class TextModificationService : DocumentColorizingTransformer
{
    private readonly Dictionary<string, TextSegmentCollection<TextModificationSegment>> _modificationSegments;

    private readonly TextView _textView;

    public TextModificationService(TextView textView)
    {
        _textView = textView;
        _modificationSegments = new Dictionary<string, TextSegmentCollection<TextModificationSegment>>();
    }

    public void ClearModification(string key)
    {
        if (!_modificationSegments.ContainsKey(key)) return;

        var copy = new TextModificationSegment[_modificationSegments[key].Count];

        _modificationSegments[key].CopyTo(copy, 0);
        foreach (var s in copy)
        {
            _modificationSegments[key].Remove(s);
            _textView.Redraw(s);
        }
    }

    public void SetModification(string key, params TextModificationSegment[] segments)
    {
        ClearModification(key);

        _modificationSegments.TryAdd(key, new TextSegmentCollection<TextModificationSegment>(_textView.Document));
        var m = _modificationSegments[key];

        foreach (var s in segments)
        {
            var overlap = m.FindOverlappingSegments(s.StartOffset, s.Length);
            if (overlap.Any())
            {
                var f = overlap.First();
                if (s.StartOffset >= f.StartOffset)
                {
                    if (s.StartOffset <= f.EndOffset) continue; //Completely overlapped
                    if (s.EndOffset > f.EndOffset + 1)
                    {
                        var endOffset = s.EndOffset;
                        s.StartOffset = f.EndOffset + 1;
                        s.EndOffset = endOffset;
                    }
                }

                if (s.StartOffset < f.StartOffset)
                    if (s.EndOffset > f.EndOffset - 1)
                        s.EndOffset = f.EndOffset - 1;

                if (s.Length <= 0) continue;
            }

            m.Add(s);
            _textView.Redraw(s.StartOffset, s.Length);
        }
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (!line.IsDeleted)
            foreach (var m in _modificationSegments)
            {
                var overlaps = m.Value.FindOverlappingSegments(line.Offset, line.Length);

                foreach (var overlap in overlaps)
                {
                    if (!TryClampToLine(overlap, line, out var startOffset, out var endOffset)) continue;
                    ChangeLinePart(startOffset, endOffset, x => ApplyChanges(x, overlap));
                }
            }
    }

    // Must not mutate the segment: it is shared by all lines it spans, and setting
    // TextSegment.StartOffset moves the segment (keeps its length) instead of trimming it.
    internal static bool TryClampToLine(ISegment segment, DocumentLine line, out int startOffset, out int endOffset)
    {
        startOffset = Math.Max(segment.Offset, line.Offset);
        endOffset = Math.Min(segment.EndOffset, line.EndOffset);
        return endOffset > startOffset;
    }

    private void ApplyChanges(VisualLineElement element, TextModificationSegment segment)
    {
        if (segment.Foreground != null) element.TextRunProperties.SetForegroundBrush(segment.Foreground);
        if (segment.Background != null) element.TextRunProperties.SetBackgroundBrush(segment.Background);
        if (segment.Decorations != null) element.TextRunProperties.SetTextDecorations(segment.Decorations);
    }
}