using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace OneWare.Studio.Desktop.Views;

public class SplashWindow : Window
{
    public SplashWindow()
    {
        // Window settings
        Width = 540;
        Height = 304;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ClipToBounds = false;
        Title = "SplashScreen";

        // Image
        var image = new Image
        {
            Width = 540,
            Height = 304,
            Stretch = Stretch.None,
            Source = new Bitmap(
                AssetLoader.Open(new Uri("avares://OneWareStudio/Assets/Startup.png"))
            )
        };

        // Border with shadow
        var border = new Border
        {
            Child = image,
            BoxShadow = new BoxShadows(
                new BoxShadow
                {
                    OffsetX = 5,
                    OffsetY = 5,
                    Blur = 10,
                    Spread = 0,
                    Color = Colors.DarkGray
                }
            )
        };

        Content = border;
    }
}