using System.Collections.Specialized;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using OneWare.Essentials.Extensions;

namespace OneWare.Essentials.EditorExtensions;

[Obsolete("Superseded by BreakPointLineNumberMargin, which puts the breakpoint on the line " +
          "number instead of adding a column of its own. Kept so that anyone using this margin " +
          "directly keeps working.")]
public class BreakPointMargin : AbstractMargin
{
    private readonly string _filePath;
    private readonly TextEditor _editor;
    private readonly BreakpointStore _manager;

    private int _previewLine;
    private bool _previewPointVisible;

    static BreakPointMargin()
    {
        FocusableProperty.OverrideDefaultValue(typeof(BreakPointMargin), true);
    }

    public BreakPointMargin(TextEditor editor, string filePath, BreakpointStore manager)
    {
        _manager = manager;
        _editor = editor;
        _filePath = filePath;

    }

    // Subscribing here rather than in the constructor: the store is shared and outlives this
    // margin, so a subscription that is never released would keep every margin of every closed
    // editor alive and redraw it on each change.
    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null) _manager.Breakpoints.CollectionChanged -= OnBreakpointsChanged;

        base.OnTextViewChanged(oldTextView, newTextView);

        if (newTextView != null) _manager.Breakpoints.CollectionChanged += OnBreakpointsChanged;
    }

    private void OnBreakpointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (TextView.VisualLinesValid)
        {
            context.FillRectangle(Brushes.Transparent,
                new Rect(0, 0, Bounds.Width, Bounds.Height));

            if (TextView.VisualLines.Count > 0)
            {
                var firstLine = TextView.VisualLines.FirstOrDefault();
                if (firstLine == null) return;

                foreach (var breakPoint in _manager.Breakpoints)
                    if (breakPoint.File == _filePath)
                    {
                        var visualLine = TextView.VisualLines.FirstOrDefault(vl =>
                            vl.FirstDocumentLine.LineNumber == breakPoint.Line);

                        if (visualLine != null)
                            context.FillRectangle(Brush.Parse("#FF3737"),
                                new Rect(
                                    Bounds.Size.Width / 4 - 1,
                                    visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0],
                                        VisualYPosition.LineTop) + Bounds.Size.Width / 1.5 / 4 -
                                    TextView.VerticalOffset,
                                    Bounds.Size.Width / 1.5,
                                    visualLine.Height / 1.5),
                                (float)visualLine.Height);
                    }

                if (_previewPointVisible)
                {
                    var visualLine =
                        TextView.VisualLines.FirstOrDefault(vl => vl.FirstDocumentLine.LineNumber == _previewLine);

                    if (visualLine != null)
                        context.FillRectangle(Brush.Parse("#E67466"),
                            new Rect(
                                Bounds.Size.Width / 4 - 1,
                                visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0],
                                    VisualYPosition.LineTop) + Bounds.Size.Width / 1.5 / 4 -
                                TextView.VerticalOffset,
                                Bounds.Size.Width / 1.5,
                                visualLine.Height / 1.5), (float)visualLine.Height);
                }
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        _previewPointVisible = true;

        var textView = TextView;

        var offset = _editor.GetOffsetFromPointerPosition(e);

        if (offset != -1)
            _previewLine =
                textView.Document.GetLineByOffset(offset).LineNumber; // convert from text line to visual line.

        Cursor = Cursor.Parse("Hand");

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _previewPointVisible = true;

        var textView = TextView;

        var offset = _editor.GetOffsetFromPointerPosition(e);

        if (offset != -1)
        {
            var lineClicked = -1;
            lineClicked =
                textView.Document.GetLineByOffset(offset).LineNumber; // convert from text line to visual line.

            var currentBreakPoint =
                _manager.Breakpoints.FirstOrDefault(bp =>
                    bp.File == _filePath && bp.Line == lineClicked);

            if (currentBreakPoint != null)
            {
                _manager.Remove(currentBreakPoint);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(_filePath))
                    _manager.Add(new BreakPoint { File = _filePath, Line = lineClicked });
            }
        }

        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (TextView != null) return new Size(TextView.DefaultLineHeight, 0);

        return new Size(0, 0);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        _previewPointVisible = false;

        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        InvalidateVisual();
        e.Handled = true;
    }
}
