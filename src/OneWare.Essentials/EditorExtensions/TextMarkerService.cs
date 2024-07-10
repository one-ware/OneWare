using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.EditorExtensions
{
    public class TextMarkerService : IBackgroundRenderer
    {
        private TextDocument _document;
        private TextSegmentCollection<DiagnosticTextMarker> _diagnosticMarkers;

        public TextMarkerService(TextDocument document)
        {
            _document = document;
            _diagnosticMarkers = new TextSegmentCollection<DiagnosticTextMarker>(document);
        }

        public void ChangeDocument(TextDocument document)
        {
            _document = document;
            _diagnosticMarkers = new TextSegmentCollection<DiagnosticTextMarker>(document);
        }

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            var visualLines = textView.VisualLines;
            if (visualLines.Count == 0) return;
            if (visualLines.First().FirstDocumentLine.IsDeleted ||
                visualLines.Last().LastDocumentLine.IsDeleted) return;
            
            var viewStart = visualLines.First().FirstDocumentLine.Offset;
            var viewEnd = visualLines.Last().LastDocumentLine.EndOffset;

            var start = Math.Min(viewStart, viewEnd);
            var end = Math.Max(viewStart, viewEnd);

            if (_diagnosticMarkers is {Count: > 0})
                foreach (var marker in _diagnosticMarkers.FindOverlappingSegments(start, end - start))
                    if (marker.Length > 0)
                        if (marker.EndOffset <= textView.Document.TextLength)
                            foreach (var r in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
                            {
                                var startPoint = r.BottomLeft;
                                var endPoint = r.BottomRight;

                                var usedPen = new Pen(marker.Brush);

                                const double offset = 2.5;

                                var count = Math.Max((int)((endPoint.X - startPoint.X) / offset) + 1, 4);

                                var geometry = new StreamGeometry();

                                using (var ctx = geometry.Open())
                                {
                                    ctx.BeginFigure(startPoint, false);

                                    foreach (var point in CreatePoints(startPoint, endPoint, offset, count))
                                        ctx.LineTo(point);

                                    ctx.EndFigure(false);
                                }

                                drawingContext.DrawGeometry(Brushes.Transparent, usedPen, geometry);
                                break;
                            }
        }

        public void Dispose()
        {
            _diagnosticMarkers.Clear();
            _diagnosticMarkers.Disconnect(_document);
        }

        private static IEnumerable<Point> CreatePoints(Point start, Point end, double offset, int count)
        {
            for (var i = 0; i < count; i++)
                yield return new Point(start.X + i * offset, start.Y - ((i + 1) % 2 == 0 ? offset : 0));
        }

        public void RemoveAll(Predicate<TextMarker> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var toRemove = _diagnosticMarkers.Where(t => predicate(t)).ToArray();

            foreach (var m in toRemove) _diagnosticMarkers.Remove(m);
        }
        
        public void SetDiagnostics(IEnumerable<ErrorListItem> diagnostics)
        {
            _diagnosticMarkers.Clear();
            
            foreach (var diag in diagnostics)
            {
                IBrush markerColor;

                switch (diag.Type)
                {
                    case ErrorType.Error:
                        markerColor = Brushes.Red;
                        break;

                    case ErrorType.Warning:
                        markerColor = Brushes.DarkGray;
                        break;

                    default:
                        markerColor = Brushes.DarkGray;
                        break;
                }

                var offset = diag.GetOffset(_document);

                var sOff = offset.startOffset;
                var eOff = offset.endOffset;
                var overlap = _diagnosticMarkers.FindOverlappingSegments(sOff, eOff);
                if (overlap.Any())
                {
                    var f = overlap.First();
                    if (sOff >= f.StartOffset)
                    {
                        if(eOff <= f.EndOffset) continue; //Completely overlapped
                        if (eOff > f.EndOffset + 1) sOff = f.EndOffset + 1;
                    }
                    if (sOff < f.StartOffset)
                    {
                        if (eOff > f.EndOffset - 1) eOff = f.EndOffset - 1;
                    }
                }

                _diagnosticMarkers.Add(new DiagnosticTextMarker(sOff, eOff){Brush = markerColor});
            }
        }

        public IEnumerable<TextMarker> GetMarkersAtOffset(int offset)
        {
            return _diagnosticMarkers.FindSegmentsContaining(offset);
        }

        public IEnumerable<TextMarker> FindOverlappingMarkers(ISegment segment)
        {
            return _diagnosticMarkers.FindOverlappingSegments(segment);
        }

        public sealed class DiagnosticTextMarker : TextMarker
        {
            public DiagnosticTextMarker(int startOffset, int endOffset) : base(startOffset, endOffset){}
        }

        public class TextMarker : TextSegment
        {
            public TextMarker(int startOffset, int endOffset)
            {
                StartOffset = startOffset < 0 ? 0 : startOffset;
                EndOffset = endOffset;
            }

            public IBrush? Brush { get; set; }
        }
    }
}