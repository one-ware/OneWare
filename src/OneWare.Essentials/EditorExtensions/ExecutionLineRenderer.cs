using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace OneWare.Essentials.EditorExtensions;

/// <summary>
/// Marks the line where a debugger halted. Drawn as its own background layer, so it stays visible
/// regardless of the text selection. Colors come from the <c>DebugExecutionLineBrush</c> and
/// <c>DebugExecutionLineAccentBrush</c> theme tokens.
/// </summary>
public class ExecutionLineRenderer : IBackgroundRenderer
{
    private const double AccentWidth = 3;

    private readonly TextView _textView;
    private int? _line;

    public ExecutionLineRenderer(TextView textView)
    {
        _textView = textView;
    }

    /// <summary>
    /// 1-based line number to mark, or <c>null</c> to clear the marker.
    /// </summary>
    public int? Line
    {
        get => _line;
        set
        {
            if (_line == value) return;
            _line = value;
            _textView.InvalidateLayer(Layer);
        }
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_line is not { } line || textView.Document == null || line < 1 || line > textView.Document.LineCount)
            return;

        var visualLine = textView.GetVisualLine(line);
        if (visualLine == null) return;

        var top = visualLine.VisualTop - textView.ScrollOffset.Y;
        var rect = new Rect(0, top, textView.Bounds.Width, visualLine.Height);

        if (FindBrush(textView, "DebugExecutionLineBrush") is { } fill) drawingContext.FillRectangle(fill, rect);

        if (FindBrush(textView, "DebugExecutionLineAccentBrush") is { } accent)
            drawingContext.FillRectangle(accent, rect.WithWidth(AccentWidth));
    }

    private static IBrush? FindBrush(TextView textView, string key)
    {
        return textView.TryFindResource(key, textView.ActualThemeVariant, out var value) ? value as IBrush : null;
    }
}
