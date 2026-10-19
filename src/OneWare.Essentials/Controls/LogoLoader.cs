using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OneWare.Essentials.Controls;

/// <summary>
/// Calm busy indicator showing the "E" of the OneWare app icon. A soft glow passes slowly through its four parts
/// (top bar, block, middle bar, bottom bar) in <see cref="Foreground" />. It only animates while it is visible and
/// attached to a window.
/// </summary>
public class LogoLoader : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<LogoLoader, IBrush?>(nameof(Foreground));

    /// <summary>Time the glow needs to pass through all parts once.</summary>
    public static readonly StyledProperty<TimeSpan> CycleDurationProperty =
        AvaloniaProperty.Register<LogoLoader, TimeSpan>(nameof(CycleDuration), TimeSpan.FromMilliseconds(2400));

    private const double MinOpacity = 0.15;

    // Parts of the app icon glyph in its original 216×216 unit space, in the order the glow passes through them.
    private static readonly Point[][] Parts =
    [
        [new(0, 0), new(215.3, 0), new(215.3, 34), new(34.3, 34), new(34.3, 54.3), new(0, 54.3)],
        [new(0, 76.9), new(61.9, 76.9), new(61.9, 138.8), new(0, 138.8)],
        [new(84.5, 90.7), new(215.3, 90.7), new(215.3, 125.1), new(84.5, 125.1)],
        [new(0, 161.4), new(34.3, 161.4), new(34.3, 181.7), new(215.3, 181.7), new(215.3, 216.1), new(0, 216.1)]
    ];

    private const double GlyphSize = 216.1;

    private readonly AnimationFrameLoop _loop;

    static LogoLoader()
    {
        AffectsRender<LogoLoader>(ForegroundProperty);
    }

    public LogoLoader()
    {
        _loop = new AnimationFrameLoop(this);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public TimeSpan CycleDuration
    {
        get => GetValue(CycleDurationProperty);
        set => SetValue(CycleDurationProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(12, 12);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _loop.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loop.Detach();
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (Foreground is not { } brush || size <= 0) return;

        var scale = size / GlyphSize;
        var origin = new Point((Bounds.Width - size) / 2, (Bounds.Height - size) / 2);
        var progress = AnimationFrameLoop.GetProgress(CycleDuration);

        for (var i = 0; i < Parts.Length; i++)
        {
            // A raised cosine per part, shifted so the glow travels through the parts in order.
            var glow = 0.5 - 0.5 * Math.Cos(2 * Math.PI * (progress - (double)i / Parts.Length));

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var points = Parts[i];
                ctx.BeginFigure(origin + points[0] * scale, true);
                for (var p = 1; p < points.Length; p++) ctx.LineTo(origin + points[p] * scale);
                ctx.EndFigure(true);
            }

            using (context.PushOpacity(MinOpacity + (1 - MinOpacity) * glow))
            {
                context.DrawGeometry(brush, null, geometry);
            }
        }

        _loop.RequestNextFrame();
    }
}
