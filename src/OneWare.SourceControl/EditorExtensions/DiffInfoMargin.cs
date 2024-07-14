using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using OneWare.SourceControl.Models;

namespace OneWare.SourceControl.EditorExtensions;

public class DiffInfoMargin : AbstractMargin
{
    private const double TextHorizontalMargin = 4.0;
    public static readonly Brush AddedBackground;
    public static readonly Brush DeletedBackground;
    public static readonly Brush BlankBackground;

    private static readonly Pen BorderlessPen;

    static DiffInfoMargin()
    {
        AddedBackground = new SolidColorBrush(Color.FromArgb(50, 150, 200, 100));

        DeletedBackground = new SolidColorBrush(Color.FromArgb(50, 175, 50, 50));

        BlankBackground = new SolidColorBrush(Color.FromArgb(10, 0xfa, 0xfa, 0xfa));

        var transparentBrush = new SolidColorBrush(Colors.Transparent);

        BorderlessPen = new Pen(transparentBrush, 0);
    }

    /// <summary>
    ///     The typeface used for rendering the line number margin.
    ///     This field is calculated in MeasureOverride() based on the FontFamily etc. properties.
    /// </summary>
    protected FontFamily Typeface { get; set; }

    /// <summary>
    ///     The font size used for rendering the line number margin.
    ///     This field is calculated in MeasureOverride() based on the FontFamily etc. properties.
    /// </summary>
    protected double EmSize { get; set; }

    public List<DiffLineModel> Lines { get; set; } = new();

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        Typeface = GetValue(TextBlock.FontFamilyProperty);
        EmSize = GetValue(TextBlock.FontSizeProperty);

        var maxLines = 2;
        if (Lines.Count > 0)
            for (var i = Lines.Count - 1; i >= 0; i--)
                if (Lines[i].Style != DiffContext.Blank && !string.IsNullOrEmpty(Lines[i].LineNumber))
                {
                    maxLines = Lines[i].LineNumber.Length + 1;
                    break;
                }

        var text = new FormattedText(new string('9', maxLines), CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Avalonia.Media.Typeface.Default, EmSize,
            GetValue(TemplatedControl.ForegroundProperty));
        return new Size(text.Width, 0);
    }

    public override void Render(DrawingContext drawingContext)
    {
        base.Render(drawingContext);

        var foreground = GetValue(TextEditor.LineNumbersForegroundProperty);
        var renderSize = Bounds.Size;

        var testPrefixWidth = new FormattedText("+", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Avalonia.Media.Typeface.Default, EmSize, foreground);

        if (Lines == null || Lines.Count == 0) return;

        var visualLines = TextView.VisualLinesValid
            ? TextView.VisualLines
            : new ReadOnlyCollection<VisualLine>(new List<VisualLine>());
        foreach (var v in visualLines)
        {
            var rcs = BackgroundGeometryBuilder.GetRectsFromVisualSegment(TextView, v, 0, 1000).ToArray();
            var linenum = v.FirstDocumentLine.LineNumber - 1;
            if (linenum >= Lines.Count) continue;

            var diffLine = Lines[linenum];

            FormattedText ft;

            if (diffLine.Style != DiffContext.Context)
            {
                var brush = default(Brush);
                switch (diffLine.Style)
                {
                    case DiffContext.Added:
                        brush = AddedBackground;
                        break;
                    case DiffContext.Deleted:
                        brush = DeletedBackground;
                        break;
                    case DiffContext.Blank:
                        brush = BlankBackground;
                        break;
                }


                foreach (var rc in rcs)
                {
                    var builder = new BackgroundGeometryBuilder();
                    builder.AddRectangle(TextView, new Rect(0, rc.Top, Bounds.Width, rc.Height));
                    drawingContext.DrawGeometry(brush, BorderlessPen, builder.CreateGeometry());
                    break;
                }
            }

            if (!string.IsNullOrEmpty(diffLine.LineNumber))
            {
                ft = new FormattedText(diffLine.LineNumber, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    Avalonia.Media.Typeface.Default, EmSize, foreground);
                //drawingContext.DrawText(ft, new Point(left, ));
                drawingContext.DrawText(ft,
                    new Point(renderSize.Width - ft.Width - testPrefixWidth.Width, rcs[0].Top));
            }

            if (!string.IsNullOrEmpty(diffLine.PrefixForStyle))
            {
                ft = new FormattedText(diffLine.PrefixForStyle, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Avalonia.Media.Typeface.Default, EmSize, foreground);

                drawingContext.DrawText(ft, new Point(renderSize.Width - ft.Width, rcs[0].Top));
            }
        }
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null) oldTextView.VisualLinesChanged -= TextViewVisualLinesChanged;
        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView != null) newTextView.VisualLinesChanged += TextViewVisualLinesChanged;
        InvalidateVisual();
    }

    private void TextViewVisualLinesChanged(object? sender, EventArgs e)
    {
        InvalidateMeasure();
    }
}