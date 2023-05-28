using Avalonia.Media;
using Avalonia.Media.Imaging;
using OneWare.Shared.Converters;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;

namespace OneWare.Core.ViewModels.Windows;

public class SplashWindowViewModel : ViewModelBase
{
    public IImage? SplashScreen { get; }

    public SplashWindowViewModel(IPaths paths)
    {
        SplashScreen = SharedConverters.PathToBitmapConverter.Convert(paths.SplashScreenPath, typeof(IBitmap), null, null) as IImage;
    }
}