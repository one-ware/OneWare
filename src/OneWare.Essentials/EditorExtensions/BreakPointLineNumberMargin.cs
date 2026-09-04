using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.EditorExtensions;

// Replaces the separate breakpoint column: breakpoints live on the line number margin, and a
// line that carries one shows the dot in place of its number, as Rider and VS Code do.
// MeasureOverride stays inherited, so the column is exactly as wide as it would be without
// breakpoints.
public class BreakPointLineNumberMargin : LineNumberMargin
{
    // Colours taken unchanged from BreakPointMargin.
    private static readonly IBrush BreakPointBrush = new SolidColorBrush(Color.Parse("#FF3737"));
    private static readonly IBrush PreviewBrush = new SolidColorBrush(Color.Parse("#E67466"));

    // Not armed at the target: grey and hollow. Two differences instead of one, so that even
    // someone who tells colours apart poorly sees from the ring that this one is not armed.
    private static readonly IBrush UnverifiedBrush = new SolidColorBrush(Color.Parse("#9E9E9E"));

    private readonly TextEditor _editor;
    private readonly string _filePath;
    private readonly BreakpointStore _store;

    // From the file type, fixed for the lifetime of the margin.
    // null means no restriction, so a language without a rule notices nothing of this.
    private readonly Regex? _breakPointableLines;

    // -1 = pointer is not over the margin
    private int _previewLine = -1;

    public BreakPointLineNumberMargin(TextEditor editor, string filePath, BreakpointStore store,
        ITypeAssistance? typeAssistance = null)
    {
        _editor = editor;
        _filePath = filePath;
        _store = store;
        _breakPointableLines = CompilePattern(typeAssistance?.BreakPointLinePattern);
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    // The pattern comes from a plugin, so an invalid expression must not disable the whole
    // margin. Report it once, then behave as if no pattern had been given.
    private static Regex? CompilePattern(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return null;

        try
        {
            return new Regex(pattern, RegexOptions.Compiled);
        }
        catch (ArgumentException exception)
        {
            ContainerLocator.Container?.Resolve<ILogger>()
                .Error($"Invalid BreakPointLinePattern '{pattern}': {exception.Message}", exception);
            return null;
        }
    }

    private static void NotifyTargetRunning()
    {
        ContainerLocator.Container?.Resolve<IWindowService>().ShowNotification(
            "Breakpoint not set",
            "The target is running. Pause it before setting or removing breakpoints.",
            NotificationType.Warning);
    }

    // Without a pattern every line carries a breakpoint. With one the line text decides, not the
    // number, so the rule stays with the file type and need not be known here.
    private bool IsBreakPointable(int lineNumber)
    {
        if (_breakPointableLines == null) return true;

        var document = _editor.Document;
        if (document == null || lineNumber < 1 || lineNumber > document.LineCount) return false;

        var line = document.GetLineByNumber(lineNumber);

        return _breakPointableLines.IsMatch(document.GetText(line.Offset, line.Length));
    }

    public override void Render(DrawingContext context)
    {
        var textView = TextView;
        if (textView is not { VisualLinesValid: true }) return;

        // Colour straight from the editor: AvaloniaEdit binds LineNumbersForeground only on the
        // margin it creates itself, not on one inserted in its place.
        var foreground = _editor.LineNumbersForeground ?? GetValue(TemplatedControl.ForegroundProperty);

        foreach (var line in textView.VisualLines)
        {
            var lineNumber = line.FirstDocumentLine.LineNumber;

            var breakPoint = FindBreakPoint(lineNumber);

            var brush = breakPoint != null ? BreakPointBrush
                : lineNumber == _previewLine ? PreviewBrush
                : null;

            if (brush != null)
            {
                // If the dot does not fit the column it shrinks, so the column never grows wider
                // than the numbers alone would make it.
                var diameter = Math.Min(Bounds.Width, line.Height * 0.75);
                var centerY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineMiddle) -
                              textView.VerticalOffset;
                var center = new Point(Bounds.Width / 2, centerY);
                var radius = diameter / 2;

                if (breakPoint is { IsVerified: false })
                {
                    // Not armed at the target, so a grey ring. It sits inside the same diameter
                    // as the filled dot, so the column keeps its width and the lines do not jump.
                    var thickness = Math.Max(1.0, radius * 0.4);
                    var inner = radius - thickness / 2;

                    context.DrawEllipse(null, new Pen(UnverifiedBrush, thickness), center, inner, inner);
                }
                else
                {
                    context.DrawEllipse(brush, null, center, radius, radius);
                }
            }
            else
            {
                var text = new FormattedText(lineNumber.ToString(CultureInfo.CurrentCulture),
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface, EmSize, foreground);
                context.DrawText(text,
                    new Point(Bounds.Width - text.Width,
                        line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) -
                        textView.VerticalOffset));
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        // Deliberately without the base call: here a click means breakpoint and nothing else,
        // so the line selection of the base class does not happen. Same as Rider and VS Code.
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (_store.IsTargetRunning)
        {
            NotifyTargetRunning();
            e.Handled = true;
            return;
        }

        var lineNumber = GetLineNumberAtPointer(e);
        if (lineNumber > 0 && !string.IsNullOrWhiteSpace(_filePath))
        {
            var existing = _store.Breakpoints.FirstOrDefault(bp => bp.File == _filePath && bp.Line == lineNumber);

            // Removing always stays possible: a breakpoint that predates a rule change, or whose
            // line has since been edited, must still be removable.
            if (existing != null) _store.Remove(existing);
            else if (IsBreakPointable(lineNumber)) _store.Add(new BreakPoint { File = _filePath, Line = lineNumber });
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var lineNumber = GetLineNumberAtPointer(e);

        // Preview only where the click would take effect: a dot that does not stay once the
        // button is released would be the most misleading feedback of all.
        if (_store.IsTargetRunning || !IsBreakPointable(lineNumber)) lineNumber = -1;

        if (lineNumber == _previewLine) return;

        _previewLine = lineNumber;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        _previewLine = -1;
        InvalidateVisual();
    }

    // Subscribing here rather than in the constructor: the subscription then lasts exactly as
    // long as the margin is attached, and a store that outlives the margin cannot keep a closed
    // editor and its document alive through it.
    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null)
        {
            _store.Breakpoints.CollectionChanged -= OnBreakpointsChanged;
            _store.VerificationChanged -= OnVerificationChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);

        if (newTextView != null)
        {
            _store.Breakpoints.CollectionChanged += OnBreakpointsChanged;
            _store.VerificationChanged += OnVerificationChanged;
        }
    }

    private void OnBreakpointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private void OnVerificationChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    // Returns the breakpoint rather than just yes/no: Render needs its state to tell an armed
    // one from one that is only set.
    private BreakPoint? FindBreakPoint(int lineNumber)
    {
        return _store.Breakpoints.FirstOrDefault(bp => bp.File == _filePath && bp.Line == lineNumber);
    }

    // Determining the line through the text view rather than through editor coordinates keeps
    // this independent of where among the left margins this one sits; below the last line, -1.
    private int GetLineNumberAtPointer(PointerEventArgs e)
    {
        var textView = TextView;
        if (textView == null) return -1;
        var visualLine = textView.GetVisualLineFromVisualTop(e.GetPosition(this).Y + textView.VerticalOffset);
        return visualLine?.FirstDocumentLine.LineNumber ?? -1;
    }
}
