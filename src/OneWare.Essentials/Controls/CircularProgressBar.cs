using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OneWare.Essentials.Controls;

/// <summary>
/// A compact circular arc progress indicator.
/// Draws a full-circle track and a sweeping arc for the current value.
/// </summary>
public class CircularProgressBar : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<CircularProgressBar, double>(nameof(Value));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<CircularProgressBar, double>(nameof(Maximum), 1.0);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<CircularProgressBar, double>(nameof(StrokeThickness), 2.0);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CircularProgressBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> ProgressBrushProperty =
        AvaloniaProperty.Register<CircularProgressBar, IBrush?>(nameof(ProgressBrush));

    static CircularProgressBar()
    {
        AffectsRender<CircularProgressBar>(
            ValueProperty,
            MaximumProperty,
            StrokeThicknessProperty,
            TrackBrushProperty,
            ProgressBrushProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? ProgressBrush
    {
        get => GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var thickness = StrokeThickness;
        var cx = w / 2.0;
        var cy = h / 2.0;
        var radius = Math.Min(cx, cy) - thickness / 2.0;
        if (radius <= 0) return;

        var trackPen = new Pen(TrackBrush ?? Brushes.Gray, thickness, lineCap: PenLineCap.Round);
        var progressPen = new Pen(ProgressBrush ?? Brushes.DodgerBlue, thickness, lineCap: PenLineCap.Round);

        // Background track — full circle
        context.DrawEllipse(null, trackPen, new Point(cx, cy), radius, radius);

        // Progress arc
        var maximum = Maximum;
        if (maximum <= 0) return;
        var fraction = Math.Clamp(Value / maximum, 0.0, 1.0);
        if (fraction <= 0) return;

        if (fraction >= 1.0)
        {
            // Full circle — draw as ellipse to avoid arc geometry edge case
            context.DrawEllipse(null, progressPen, new Point(cx, cy), radius, radius);
            return;
        }

        var startAngle = -Math.PI / 2.0;              // top of circle
        var sweepAngle = fraction * 2.0 * Math.PI;
        var endAngle = startAngle + sweepAngle;

        var start = new Point(
            cx + radius * Math.Cos(startAngle),
            cy + radius * Math.Sin(startAngle));
        var end = new Point(
            cx + radius * Math.Cos(endAngle),
            cy + radius * Math.Sin(endAngle));

        var geo = new StreamGeometry();
        using (var geoCtx = geo.Open())
        {
            geoCtx.BeginFigure(start, false);
            geoCtx.ArcTo(
                end,
                new Size(radius, radius),
                0,
                sweepAngle > Math.PI,
                SweepDirection.Clockwise);
        }

        context.DrawGeometry(null, progressPen, geo);
    }
}


