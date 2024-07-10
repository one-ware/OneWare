using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Essentials.Controls
{
    public class ScrollBarRenderer : Control
    {
        public static readonly StyledProperty<ScrollInfoContext?> ScrollInfoProperty =
            AvaloniaProperty.Register<ScrollBarRenderer, ScrollInfoContext?>(nameof(ScrollInfo));

        public static readonly StyledProperty<TextEditor> CodeBoxProperty =
            AvaloniaProperty.Register<ScrollBarRenderer, TextEditor>(nameof(CodeBox));

        public ScrollBarRenderer()
        {
            ClipToBounds = true;

            this.GetObservable(ScrollInfoProperty).Subscribe(x =>
            {
                if (x != null)
                {
                    x.Changed += (o, i) =>
                    {
                        Redraw();
                    };
                }
                Redraw();
            });
        }

        private IDisposable? sub;
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == ScrollInfoProperty)
            {
                sub?.Dispose();
                if (ScrollInfo != null)
                {
                    sub = Observable.FromEventPattern(ScrollInfo, nameof(ScrollInfo.Changed)).Subscribe(x =>
                    {
                        Redraw();
                    });
                }
            }
            base.OnPropertyChanged(change);
        }

        public ScrollInfoContext?  ScrollInfo
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
                foreach (var scrollInfo in ScrollInfo.InfoLines)
                {
                    if (docHeight * 2 < Bounds.Height)
                    {
                        var vT = CodeBox.TextArea.TextView.GetVisualLine(scrollInfo.Line);

                        if (vT != null)
                        {
                            context.FillRectangle(scrollInfo.Brush,
                                new Rect(0, vT.VisualTop, Bounds.Width,vT.Height));
                        }
                    }
                    else
                    {
                        context.FillRectangle(scrollInfo.Brush,
                            new Rect(0, Bounds.Height * (scrollInfo.Line / (double)fakeLineCount), Bounds.Width,
                                scrollBarLineHeight));
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