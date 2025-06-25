using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OneWare.Essentials.Services;

namespace OneWare.Demo.Desktop.ViewModels;

public class SplashWindowViewModel
{
    public SplashWindowViewModel()
    {
        SplashScreen = new Bitmap(AssetLoader.Open(new Uri("avares://OneWare.Demo.Desktop/Assets/Startup.jpg")));
    }

    public IImage? SplashScreen { get; }
}