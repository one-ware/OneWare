using System;
using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using OneWare.Core.Data;

namespace OneWare.Studio.Desktop.Views;

public class SplashWindow : Window
{
    private const double CardWidth = 540;
    private const double CardHeight = 304;
    private const double ShadowMargin = 16;
    private const double IndicatorWidth = 140;
    private static readonly Color BackgroundColor = Color.Parse("#2B2D30");
    private static readonly Color BorderColor = Color.Parse("#3C3F44");
    private static readonly Color ForegroundColor = Color.Parse("#DFE1E5");
    private static readonly Color MutedColor = Color.Parse("#9DA0A8");
    private static readonly Color AccentColor = Color.Parse("#009688");

    // Paths from branding/logo-oneware.svg, cropped to the glyph bounds (35.6, 29.4, 368.2 x 52.6).
    private const double LogoOffsetX = -35.6;
    private const double LogoOffsetY = -29.4;
    private const double LogoWidth = 368.2;
    private const double LogoHeight = 52.6;

    private const string LogoWhitePath =
        "M180.7,51.5 h31.8 v8.4 h-31.8 Z " +
        "M212.5,29.4 L160.1,29.4 L160.1,42.6 L168.5,42.6 L168.5,37.7 L212.5,37.7 Z " +
        "M168.5,73.6 L168.5,68.7 L160.1,68.7 L160.1,82 L212.5,82 L212.5,73.6 Z " +
        "M160.1,48.1 h15.1 v15.1 h-15.1 Z " +
        "M35.6,29.4 v44.3 V82 h8.4 h35.7 H88 v-8.4 V37.8 v-8.4 Z M79.6,73.6 H43.9 V37.8 h35.7 Z " +
        "M142.1,82 L150.3,82 L150.3,29.4 L141.9,29.4 L141.9,69.3 L106.2,29.4 L97.9,29.4 L97.9,82 " +
        "L106.3,82 L106.3,41.9 L141.9,82 Z " +
        "M301.1,38.1l-11.8,35.1h-3.9l-10.3-29.9l-10.3,29.9h-3.9l-11.8-35.1h3.8L263,68.3l10.5-30.2h3.5l10.3,30.3" +
        "l10.3-30.3H301.1z " +
        "M328.8,63.9h-19.6l-4.2,9.4h-3.9l16-35.1h3.7l16,35.1H333L328.8,63.9z M327.5,60.8L319.1,42l-8.4,18.9H327.5z " +
        "M366.7,73.2l-8-11.3c-0.9,0.1-1.8,0.2-2.8,0.2h-9.4v11.1h-3.7V38.1h13.1c8.9,0,14.3,4.5,14.3,12" +
        "c0,5.5-2.9,9.4-8,11l8.6,12H366.7z M366.5,50.2c0-5.6-3.7-8.8-10.7-8.8h-9.3v17.6h9.3C362.8,58.9,366.5,55.7,366.5,50.2z " +
        "M403.8,70v3.2H379V38.1h24.1v3.2h-20.4v12.5h18.1V57h-18.1v13H403.8z";

    private const string LogoAccentPath = "M224.3,48.2 h15.1 v15.1 h-15.1 Z";

    private static readonly CornerRadius Radius = new(8);
    private readonly Border _card;
    private readonly Border _indicator;

    public SplashWindow()
    {
        Width = CardWidth + ShadowMargin * 2;
        Height = CardHeight + ShadowMargin * 2;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = "OneWare Studio";
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

        var baseLayer = new Border
        {
            Background = new SolidColorBrush(BackgroundColor)
        };

        var highlightLayer = new Border
        {
            Background = CreateGlow(0.22, 0.22, 164, Color.FromArgb(
                13, ForegroundColor.R, ForegroundColor.G, ForegroundColor.B))
        };
        var glowLayer = new Border
        {
            Background = CreateGlow(0.78, 0.30, 198, Color.FromArgb(
                31, AccentColor.R, AccentColor.G, AccentColor.B))
        };

        var grid = new GridPattern
        {
            Opacity = 0.85,
            OpacityMask = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.25, 0.07, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.75, 0.93, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Colors.Transparent, 0.15),
                    new GradientStop(Colors.Black, 0.33),
                    new GradientStop(Colors.Black, 0.67),
                    new GradientStop(Colors.Transparent, 0.85)
                }
            }
        };

        var logo = new Viewbox
        {
            Width = 300,
            Stretch = Stretch.Uniform,
            Child = new Canvas
            {
                Width = LogoWidth,
                Height = LogoHeight,
                Children =
                {
                    CreateLogoPath(LogoWhitePath, new SolidColorBrush(ForegroundColor)),
                    CreateLogoPath(LogoAccentPath, new SolidColorBrush(AccentColor))
                }
            }
        };

        var product = new TextBlock
        {
            Text = "STUDIO",
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            LetterSpacing = 9,
            Foreground = new SolidColorBrush(MutedColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(9, 18, 0, 0)
        };

        var branding = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 0),
            Children = { logo, product }
        };

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(20, 0, 20, 16),
            Children =
            {
                new TextBlock
                {
                    Text = "Starting\u2026",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(MutedColor, 0.8)
                },
                new TextBlock
                {
                    Text = $"v{Global.VersionCode}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(MutedColor, 0.8),
                    [Grid.ColumnProperty] = 1
                }
            }
        };

        _indicator = new Border
        {
            Width = IndicatorWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, AccentColor.R, AccentColor.G, AccentColor.B), 0),
                    new GradientStop(AccentColor, 0.5),
                    new GradientStop(Color.FromArgb(0, AccentColor.R, AccentColor.G, AccentColor.B), 1)
                }
            }
        };

        var progressTrack = new Border
        {
            Height = 2,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = new SolidColorBrush(ForegroundColor, 0.06),
            ClipToBounds = true,
            Child = _indicator
        };

        var innerStroke = new Border
        {
            CornerRadius = Radius,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(BorderColor),
            IsHitTestVisible = false
        };

        var clip = new Border
        {
            CornerRadius = Radius,
            ClipToBounds = true,
            Child = new Panel
            {
                Children = { baseLayer, highlightLayer, glowLayer, grid, branding, footer, progressTrack, innerStroke }
            }
        };

        _card = new Border
        {
            Margin = new Thickness(ShadowMargin),
            CornerRadius = Radius,
            Background = new SolidColorBrush(BackgroundColor),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetY = 6,
                Blur = 18,
                Color = Color.FromArgb(0x70, BackgroundColor.R, BackgroundColor.G, BackgroundColor.B)
            }),
            Child = clip
        };

        Content = _card;

        Opened += (_, _) => ApplyTransparencyFallback();
        _indicator.Loaded += (_, _) => StartIndicatorAnimation();
    }

    private void ApplyTransparencyFallback()
    {
        if (ActualTransparencyLevel != WindowTransparencyLevel.None) return;

        // Without a compositor the transparent margin would render as a solid frame.
        Width = CardWidth;
        Height = CardHeight;
        _card.Margin = default;
        _card.BoxShadow = default;
    }

    // Runs on the compositor so it keeps moving while initialization blocks the UI thread.
    private void StartIndicatorAnimation()
    {
        if (ElementComposition.GetElementVisual(_indicator) is not { } visual) return;

        var trackWidth = (float)CardWidth;
        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Target = nameof(CompositionVisual.Offset);
        animation.InsertKeyFrame(0f, new Vector3(-(float)IndicatorWidth, 0, 0));
        animation.InsertKeyFrame(1f, new Vector3(trackWidth, 0, 0), new SineEaseInOut());
        animation.Duration = TimeSpan.FromMilliseconds(1600);
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation(nameof(CompositionVisual.Offset), animation);
    }

    private static RadialGradientBrush CreateGlow(double centerX, double centerY, double radius, Color color)
    {
        var center = new RelativePoint(centerX, centerY, RelativeUnit.Relative);
        return new RadialGradientBrush
        {
            Center = center,
            GradientOrigin = center,
            RadiusX = new RelativeScalar(radius, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(radius, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(color, 0),
                new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1)
            }
        };
    }

    private static Path CreateLogoPath(string data, IBrush fill)
    {
        return new Path
        {
            Data = Geometry.Parse(data),
            Fill = fill,
            [Canvas.LeftProperty] = LogoOffsetX,
            [Canvas.TopProperty] = LogoOffsetY
        };
    }

    private sealed class GridPattern : Control
    {
        private const double CellSize = 27;
        private static readonly IPen LinePen = new Pen(new SolidColorBrush(Color.FromArgb(26, 0, 200, 170)));

        public override void Render(DrawingContext context)
        {
            var size = Bounds.Size;

            for (var x = CellSize; x < size.Width; x += CellSize)
                context.DrawLine(LinePen, new Point(x + 0.5, 0), new Point(x + 0.5, size.Height));

            for (var y = CellSize; y < size.Height; y += CellSize)
                context.DrawLine(LinePen, new Point(0, y + 0.5), new Point(size.Width, y + 0.5));
        }
    }
}