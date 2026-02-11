using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace OneWare.Studio.Styles;

public partial class StudioStyles : Avalonia.Styling.Styles
{
    public StudioStyles()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
