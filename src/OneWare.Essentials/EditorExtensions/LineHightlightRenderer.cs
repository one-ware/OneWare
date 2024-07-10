using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;

namespace OneWare.Essentials.EditorExtensions
{
    public class LineHighlightRenderer : IBackgroundRenderer
    {
        private readonly TextEditor _textEditor;

        private readonly bool _manualMode;

        public IBrush BackgroundBrush = new SolidColorBrush(Color.FromArgb(10, 0x0c, 0x0c, 0x0c));
        public IBrush BorderBrush = new SolidColorBrush(Color.FromArgb(255, 57, 57, 57));

        public LineHighlightRenderer(TextEditor textEditor, bool manualMode = false)
        {
            _textEditor = textEditor;

            BorderPen = new Pen(BackgroundBrush);

            this._manualMode = manualMode;
        }

        public Pen BorderPen { get; set; }

        public List<int> Lines { get; } = new();

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_textEditor.Document != null)
            {
                if (_manualMode)
                {
                    foreach (var line in Lines) DrawLine(line, textView, drawingContext);
                }
                else
                {
                    if (_textEditor.SelectionLength == 0 && _textEditor.CaretOffset != -1 &&
                        _textEditor.CaretOffset <= textView.Document.TextLength)
                    {
                        var currentLine = textView.Document.GetLocation(_textEditor.CaretOffset).Line;

                        DrawLine(currentLine, textView, drawingContext);
                    }
                }
            }
        }

        public void DrawLine(int line, TextView textView, DrawingContext drawingContext)
        {
            var visualLine = textView.GetVisualLine(line);
            if (visualLine == null) return;

            var builder = new BackgroundGeometryBuilder();

            var linePosY = visualLine.VisualTop - textView.ScrollOffset.Y;
            var lineBottom = linePosY + visualLine.Height;

            var pixelSize = PixelSnapHelpers.GetPixelSize(textView);


            var x = PixelSnapHelpers.PixelAlign(0, pixelSize.Width);
            var y = PixelSnapHelpers.PixelAlign(linePosY, pixelSize.Height);
            var x2 = PixelSnapHelpers.PixelAlign(textView.Bounds.Width - pixelSize.Width, pixelSize.Width);
            var y2 = PixelSnapHelpers.PixelAlign(lineBottom, pixelSize.Height);

            builder.AddRectangle(textView, new Rect(new Point(x, y), new Point(x2, y2)));

            var geometry = builder.CreateGeometry();
            if (geometry != null) drawingContext.DrawGeometry(BackgroundBrush, BorderPen, geometry);
        }
    }
}