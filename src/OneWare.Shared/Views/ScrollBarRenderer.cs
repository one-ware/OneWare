using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;

namespace OneWare.Shared.Views
{
    public class ScrollBarRenderer : Control
    {
        public static readonly StyledProperty<Dictionary<IBrush, int[]>?> ScrollInfoProperty =
            AvaloniaProperty.Register<ScrollBarRenderer, Dictionary<IBrush, int[]>?>(nameof(ScrollInfo));

        public static readonly StyledProperty<TextEditor> CodeBoxProperty =
            AvaloniaProperty.Register<ScrollBarRenderer, TextEditor>(nameof(CodeBox));

        public ScrollBarRenderer()
        {
            ClipToBounds = true;

            this.GetObservable(ScrollInfoProperty).Subscribe(x =>
            {
                Redraw();
            });
        }

        public Dictionary<IBrush, int[]>?  ScrollInfo
        {
            get => GetValue(ScrollInfoProperty);
            set => SetValue(ScrollInfoProperty, value);
        }

        //Total Height in Lines
        public TextEditor CodeBox
        {
            get => GetValue(CodeBoxProperty);
            set => SetValue(CodeBoxProperty, value);
        }

        #region Rendering

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (CodeBox?.Document == null) return;
            
            var docHeight = CodeBox.TextArea.TextView.DocumentHeight;

            var fakeLineCount = CodeBox.Document.LineCount;
            if (CodeBox.Options.AllowScrollBelowDocument) fakeLineCount += (int)(Bounds.Height / CodeBox.FontSize);
            var scrollBarLineHeight = Bounds.Height / CodeBox.Document.LineCount;
            scrollBarLineHeight = scrollBarLineHeight >= 3 ? scrollBarLineHeight : 3;
            
            if (ScrollInfo != null)
                foreach (var scrollInfo in ScrollInfo)
                {
                    var brush = scrollInfo.Key;
                    var lines = scrollInfo.Value;

                    foreach (var line in lines)
                    {
                        if (docHeight < Bounds.Height)
                        {
                            var vT = CodeBox.TextArea.TextView.GetVisualLine(line);

                            if (vT != null)
                            {
                                context.FillRectangle(brush,
                                    new Rect(0, vT.VisualTop, Bounds.Width,vT.Height));
                            }
                        }
                        else
                        {
                            context.FillRectangle(brush,
                                new Rect(0, Bounds.Height * (line / (double)fakeLineCount), Bounds.Width,
                                    scrollBarLineHeight));
                        }
                    }
                }
        }

        public void Redraw()
        {
            _ = Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        }

        #endregion
    }
}