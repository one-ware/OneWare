using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace OneWare.Shared.EditorExtensions
{
    public class LineColorizer : DocumentColorizingTransformer
    {
        private readonly IBrush? _background;
        private readonly IBrush? _color;
        private readonly int _lineNumber;

        public LineColorizer(int lineNumber, IBrush? color = null, IBrush? background = null, string id = "")
        {
            this._lineNumber = lineNumber;
            this._color = color;
            this._background = background;
            Id = id;
        }

        public string Id { get; }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (!line.IsDeleted && line.LineNumber == _lineNumber)
                ChangeLinePart(line.Offset, line.EndOffset, ApplyChanges);
        }

        private void ApplyChanges(VisualLineElement element)
        {
            // This is where you do anything with the line
            if (_color != null) element.TextRunProperties.SetForegroundBrush(_color);
            if (_background != null) element.BackgroundBrush = _background;
        }
    }
}