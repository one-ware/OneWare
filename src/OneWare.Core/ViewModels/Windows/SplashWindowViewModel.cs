using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OneWare.Shared.Services;

namespace OneWare.Core.ViewModels.Windows;

public class SplashWindowViewModel
{
    public IImage? SplashScreen { get; }

    public SplashWindowViewModel(IPaths paths)
    {
        SplashScreen = new Bitmap(AssetLoader.Open(new Uri(paths.SplashScreenPath)));
    }
}