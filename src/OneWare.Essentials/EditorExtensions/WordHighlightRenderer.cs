using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace OneWare.Essentials.EditorExtensions
{
    public class WordHighlightRenderer : IBackgroundRenderer
    {
        public static readonly Color DefaultBackground = Color.FromArgb(20, 0, 200, 255);
        public static readonly Color DefaultBorder = Color.FromArgb(150, 0, 200, 255);
        
        private readonly TextView _textView;
        private Brush? _backgroundBrush;
        private Pen? _borderPen;
        
        public WordSearchResult? Result { get; set; }

        public WordHighlightRenderer(TextView textView)
        {
            this._textView = textView ?? throw new ArgumentNullException("textView");
            this._textView.BackgroundRenderers.Add(this);
        }

        public KnownLayer Layer => KnownLayer.Caret;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (Result == null || Result.WordOffset.Count < 1)
                return;

            var builder = new BackgroundGeometryBuilder
            {
                CornerRadius = 1,
                AlignToWholePixels = true
            };

            for (var i = 0; i < Result.WordOffset.Count; i++)
                if (Result.WordLength.ElementAt(i) > 1)
                {
                    builder.AddSegment(textView,
                        new TextSegment
                            { StartOffset = Result.WordOffset.ElementAt(i), Length = Result.WordLength.ElementAt(i) });
                    builder.CloseFigure(); // prevent connecting the two segments
                }

            var geometry = builder.CreateGeometry();

            if (_borderPen == null)
                UpdateColors(DefaultBackground, DefaultBackground);

            if (geometry != null) drawingContext.DrawGeometry(_backgroundBrush, _borderPen, geometry);
        }

        public void SetHighlight(WordSearchResult? result)
        {
            if (this.Result != result)
            {
                this.Result = result;
                _textView.InvalidateLayer(Layer);
            }
        }

        private void UpdateColors(Color background, Color foreground)
        {
            _borderPen = new Pen(new SolidColorBrush(foreground));
            //this.borderPen.Freeze();

            _backgroundBrush = new SolidColorBrush(background);
            //this.backgroundBrush.Freeze();
        }

        public static void ApplyCustomizationsToRendering(WordHighlightRenderer renderer,
            IEnumerable<Color> customizations)
        {
            renderer.UpdateColors(DefaultBackground, DefaultBorder);
            foreach (var color in customizations)
            {
                //if (color.Name == BracketHighlight) {
                renderer.UpdateColors(color, color);
                //                    renderer.UpdateColors(color.Background ?? Colors.Blue, color.Foreground ?? Colors.Blue);
                // 'break;' is necessary because more specific customizations come first in the list
                // (language-specific customizations are first, followed by 'all languages' customizations)
                break;
                //}
            }
        }
        
        public static WordSearchResult SearchSelectedWord(IDocument doc, int caretOffset)
        {
            return new WordSearchResult(new List<int>(), new List<int>());
        }
    }

    public class WordSearchResult
    {
        public WordSearchResult(List<int> wordOffset, List<int> wordLength)
        {
            this.WordLength = wordLength;
            this.WordOffset = wordOffset;
        }

        public List<int> WordOffset { get; }

        public List<int> WordLength { get; }
    }
}