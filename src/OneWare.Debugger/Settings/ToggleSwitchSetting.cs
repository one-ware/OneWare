using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using OneWare.Essentials.Models;

namespace OneWare.Debugger.Settings;

public sealed class ToggleSwitchSetting : CustomSetting
{
    private const string OnBrushKey = "ThemeAccentBrush";
    private const string OffBrushKey = "ThemeForegroundLowBrush";

    public ToggleSwitchSetting(string title, bool defaultValue, string? hoverDescription = null)
        : base(defaultValue)
    {
        var toggle = new ToggleSwitch
        {
            Content = title,
            IsThreeState = false,
            IsChecked = defaultValue,
            OnContent = StateLabel("On", OnBrushKey),
            OffContent = StateLabel("Off", OffBrushKey)
        };

        toggle[!ToggleSwitch.IsCheckedProperty] =
            new Binding(nameof(Value)) { Source = this, Mode = BindingMode.TwoWay };

        toggle.Styles.Add(CheckedAccentStyle());

        if (hoverDescription is not null) ToolTip.SetTip(toggle, hoverDescription);

        Control = toggle;
    }

    public override object Value
    {
        get => base.Value;
        set => base.Value = value is true;
    }

    private static TextBlock StateLabel(string text, string brushKey)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(brushKey);
        return label;
    }

    private static Style CheckedAccentStyle()
    {
        var style = new Style(x => x.OfType<ToggleSwitch>()
            .Class(":checked")
            .Template()
            .OfType<Border>()
            .Name("OuterBorder"));

        style.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension(OnBrushKey)));

        return style;
    }
}
