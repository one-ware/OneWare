using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OneWare.Shared.Converters;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;

namespace OneWare.Core.ViewModels.Windows;

public class SplashWindowViewModel : ViewModelBase
{
    public IImage? SplashScreen { get; }

    public SplashWindowViewModel(IPaths paths)
    {
        SplashScreen = new Bitmap(AssetLoader.Open(new Uri(paths.SplashScreenPath)));
    }
}