using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace OneWare.Essentials.Styles;

public partial class StyleGallery : UserControl
{
    public StyleGallery()
    {
        InitializeComponent();
        LoadIcons();
    }

    private void LoadIcons()
    {
        if (AvaloniaXamlLoader.Load(new Uri("avares://OneWare.Essentials/Styles/Icons.axaml")) is not ResourceDictionary icons)
            return;

        foreach (var key in icons.Keys.OfType<string>().Order())
        {
            if (icons[key] is not Geometry geometry) continue;
            var item = new StackPanel
            {
                Width = 84,
                Spacing = 2,
                Children =
                {
                    new PathIcon { Data = geometry },
                    new TextBlock
                    {
                        Text = key["Icon.".Length..],
                        Classes = { "caption" },
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                }
            };
            ToolTip.SetTip(item, key);
            IconPanel.Children.Add(item);
        }
    }
}
