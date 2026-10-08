using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.Controls;

public partial class HyperLink : UserControl
{
    public static readonly StyledProperty<string> UrlProperty =
        AvaloniaProperty.Register<HyperLink, string>(nameof(Url));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<HyperLink, string>(nameof(Label));

    public static readonly StyledProperty<TextDecorationCollection> TextDecorationsProperty =
        AvaloniaProperty.Register<HyperLink, TextDecorationCollection>(nameof(TextDecorations));

    public HyperLink()
    {
        InitializeComponent();
    }

    public string Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public TextDecorationCollection TextDecorations
    {
        get => GetValue(TextDecorationsProperty);
        set => SetValue(TextDecorationsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);

        // Inside an InlineUIContainer, Avalonia treats the control's bottom as its baseline unless BaselineOffset
        // is set, which lifts the link above the surrounding text and makes the line taller.
        var baseline = Padding.Top + BorderThickness.Top + PartButton.Padding.Top + PartButton.BorderThickness.Top +
                       Urltext.TextLayout.Baseline;
        TextBlock.SetBaselineOffset(this, baseline);

        return size;
    }

    public void Open_Click(object? sender, RoutedEventArgs e)
    {
        if (File.Exists(Url))
        {
            _ = ContainerLocator.Container.Resolve<IMainDockService>().OpenFileAsync(Url);
        }
        else
        {
            PlatformHelper.OpenHyperLink(Url);
        }
    }
}
