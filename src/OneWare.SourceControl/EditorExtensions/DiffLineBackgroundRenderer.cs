using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using OneWare.SourceControl.Models;

namespace OneWare.SourceControl.EditorExtensions
{
    public class DiffLineBackgroundRenderer : IBackgroundRenderer
    {
        private static readonly Pen BorderlessPen;
        private readonly Pen? _texturePen;
        public List<DiffLineModel>? Lines { get; set; }
        
        static DiffLineBackgroundRenderer()
        {
            var transparentBrush = new SolidColorBrush(Colors.Transparent);

            BorderlessPen = new Pen(transparentBrush, 0);
        }

        public DiffLineBackgroundRenderer()
        {
            if (Application.Current == null) throw new NullReferenceException(nameof(Application.Current));

            var textureBrush = Application.Current.FindResource( Application.Current.RequestedThemeVariant, "ThemeForegroundBrush") as SolidColorBrush;
            if (textureBrush == null) throw new NullReferenceException("ThemeForegroundBrush not defined");
            
            _texturePen =
                new Pen(
                    new SolidColorBrush(textureBrush.Color),
                    0.4);
        }


        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (Lines == null) return;
            
            foreach (var v in textView.VisualLines)
            {
                var linenum = v.FirstDocumentLine.LineNumber - 1;
                if (linenum >= Lines.Count) continue;

                var line = Lines[linenum];

                if (line.Style == DiffContext.Context) continue;

                var brush = default(Brush);
                switch (line.Style)
                {
                    case DiffContext.Added:
                        brush = DiffInfoMargin.AddedBackground;
                        break;
                    case DiffContext.Deleted:
                        brush = DiffInfoMargin.DeletedBackground;
                        break;
                    case DiffContext.Blank:
                        brush = DiffInfoMargin.BlankBackground;
                        break;
                }

                var builder = new BackgroundGeometryBuilder();

                var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, v, 0, 1000).First();
                builder.AddRectangle(textView, new Rect(0, rc.Top, textView.Bounds.Width, rc.Height));
                drawingContext.DrawGeometry(brush, BorderlessPen, builder.CreateGeometry());

                builder = new BackgroundGeometryBuilder();

                var differences = line.LineDiffs;

                foreach (var difference in differences)
                    builder.AddSegment(textView,
                        new TextSegment
                            { StartOffset = v.StartOffset + difference.Offset, Length = difference.Length });
                if (differences.Count > 0) drawingContext.DrawGeometry(brush, BorderlessPen, builder.CreateGeometry());

                if (line.Style == DiffContext.Blank)
                    for (var i = -rc.Height + textView.ScrollOffset.X;
                        i < textView.Bounds.Width + textView.ScrollOffset.X;
                        i += rc.Height / 2)
                        drawingContext.DrawLine(_texturePen, new Point(rc.BottomLeft.X + i, rc.BottomLeft.Y),
                            new Point(rc.TopLeft.X + rc.Height + i, rc.TopRight.Y));
            }
        }

        public KnownLayer Layer => KnownLayer.Background;
    }

    public struct LineDifferenceOffset
    {
        public int Offset { get; }
        public int Length { get; }

        public LineDifferenceOffset(int offset, int length)
        {
            this.Offset = offset;
            this.Length = length;
        }
    }
}