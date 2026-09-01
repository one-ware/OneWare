using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace OneWare.Essentials.EditorExtensions;

// Ersetzt die eigene Breakpoint-Spalte: Breakpoints liegen auf der Zeilennummernspalte,
// bei gesetztem Breakpoint weicht die Zahl dem Punkt (Verhalten wie Rider).
// MeasureOverride bleibt geerbt -> die Spalte ist exakt so breit wie ohne Breakpoints.
public class BreakPointLineNumberMargin : LineNumberMargin
{
    // Farben unveraendert aus der bisherigen BreakPointMargin uebernommen
    private static readonly IBrush BreakPointBrush = new SolidColorBrush(Color.Parse("#FF3737"));
    private static readonly IBrush PreviewBrush = new SolidColorBrush(Color.Parse("#E67466"));

    // Vom Ziel abgelehnt: grau und hohl. Zwei Unterschiede statt einem -> auch wer Farben
    // schlecht unterscheidet, sieht am Ring, dass dieser Haltepunkt nicht scharf ist.
    private static readonly IBrush UnverifiedBrush = new SolidColorBrush(Color.Parse("#9E9E9E"));

    private readonly TextEditor _editor;
    private readonly string _filePath;
    private readonly BreakpointStore _store;

    // -1 = Zeiger nicht ueber der Spalte
    private int _previewLine = -1;

    public BreakPointLineNumberMargin(TextEditor editor, string filePath, BreakpointStore store)
    {
        _editor = editor;
        _filePath = filePath;
        _store = store;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public override void Render(DrawingContext context)
    {
        var textView = TextView;
        if (textView is not { VisualLinesValid: true }) return;

        // Farbe direkt vom Editor -> die Bindung an LineNumbersForeground legt AvaloniaEdit
        // nur an der selbst erzeugten LineNumberMargin an, nicht an dieser eingesetzten
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
                // Passt der Punkt nicht in die Spalte, schrumpft der Punkt ->
                // die Spalte wird nie breiter, als die Zahlen sie machen
                var diameter = Math.Min(Bounds.Width, line.Height * 0.75);
                var centerY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineMiddle) -
                              textView.VerticalOffset;
                var center = new Point(Bounds.Width / 2, centerY);
                var radius = diameter / 2;

                if (breakPoint is { IsVerified: false })
                {
                    // Vom Ziel abgelehnt -> grauer Ring. Er liegt innerhalb desselben
                    // Durchmessers wie der gefuellte Punkt, damit die Spalte gleich breit
                    // bleibt und die Zeilen nicht springen.
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
        // Bewusst ohne base-Aufruf: der Klick bedeutet hier ausschliesslich Breakpoint,
        // das Zeilenmarkieren der Basisklasse entfaellt (Verhalten wie Rider/VS Code)
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var lineNumber = GetLineNumberAtPointer(e);
        if (lineNumber > 0 && !string.IsNullOrWhiteSpace(_filePath))
        {
            var existing = _store.Breakpoints.FirstOrDefault(bp => bp.File == _filePath && bp.Line == lineNumber);
            if (existing != null) _store.Remove(existing);
            else _store.Add(new BreakPoint { File = _filePath, Line = lineNumber });
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var lineNumber = GetLineNumberAtPointer(e);
        if (lineNumber == _previewLine) return;
        _previewLine = lineNumber;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        _previewLine = -1;
        InvalidateVisual();
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        // An- und Abmelden hier statt im Konstruktor -> der anwendungsweite Store lebt bis
        // zum Programmende und hielte sonst jeden geschlossenen Editor samt Dokument fest
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

    // Liefert den Breakpoint statt nur "ja/nein" -> Render braucht seinen Zustand, um zwischen
    // bestaetigt und abgelehnt zu unterscheiden.
    private BreakPoint? FindBreakPoint(int lineNumber)
    {
        return _store.Breakpoints.FirstOrDefault(bp => bp.File == _filePath && bp.Line == lineNumber);
    }

    // Zeile ueber die Textansicht bestimmen statt ueber Editor-Koordinaten -> haengt nicht
    // davon ab, wo unter den Randspalten diese Margin sitzt; unterhalb der letzten Zeile -1
    private int GetLineNumberAtPointer(PointerEventArgs e)
    {
        var textView = TextView;
        if (textView == null) return -1;
        var visualLine = textView.GetVisualLineFromVisualTop(e.GetPosition(this).Y + textView.VerticalOffset);
        return visualLine?.FirstDocumentLine.LineNumber ?? -1;
    }
}
