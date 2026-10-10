using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OneWare.Essentials.Controls;

/// <summary>
/// <see cref="TextBlock" /> with a soft highlight sweeping from left to right, for status texts of running work.
/// The text rests dimmed and brightens to its full <see cref="TextBlock.Foreground" /> under the highlight. It uses
/// the same clock and default cycle as <see cref="LogoLoader" />, so both move together.
/// </summary>
public class ShimmerTextBlock : TextBlock
{
    public static readonly StyledProperty<TimeSpan> CycleDurationProperty =
        LogoLoader.CycleDurationProperty.AddOwner<ShimmerTextBlock>();

    private const double RestOpacity = 0.45;
    private const double HighlightHalfWidth = 36;

    private readonly AnimationFrameLoop _loop;

    public ShimmerTextBlock()
    {
        _loop = new AnimationFrameLoop(this);
    }

    public TimeSpan CycleDuration
    {
        get => GetValue(CycleDurationProperty);
        set => SetValue(CycleDurationProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

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

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        var bounds = new Rect(Bounds.Size);
        // The highlight enters left of the text and leaves right of it, so each sweep starts and ends dimmed.
        var travel = bounds.Width + 2 * HighlightHalfWidth;
        var center = -HighlightHalfWidth + travel * AnimationFrameLoop.GetProgress(CycleDuration);

        var rest = Color.FromArgb((byte)(RestOpacity * 255), 0, 0, 0);
        var mask = new LinearGradientBrush
        {
            // Absolute points with the default pad spread keep the text dimmed outside the highlight.
            StartPoint = new RelativePoint(center - HighlightHalfWidth, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(center + HighlightHalfWidth, 0, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(rest, 0),
                new GradientStop(Colors.Black, 0.5),
                new GradientStop(rest, 1)
            }
        };

        using (context.PushOpacityMask(mask, bounds))
        {
            base.RenderTextLayout(context, origin);
        }

        _loop.RequestNextFrame();
    }
}
