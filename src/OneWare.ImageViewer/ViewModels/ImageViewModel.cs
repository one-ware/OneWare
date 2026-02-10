using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ImageViewer.ViewModels;

public class ImageViewModel : ExtendedDocument
{
    private IImage? _image;

    public ImageViewModel(string fullPath, IProjectExplorerService projectExplorerService,
        IMainDockService mainDockService, IFileIconService fileIconService,
        IWindowService windowService) :
        base(fullPath, fileIconService, projectExplorerService, mainDockService, windowService)
    {
    }

    public IImage? Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }

    protected override void UpdateCurrentFile(string? oldPath)
    {
        try
        {
            switch (Path.GetExtension(FullPath).ToLower())
            {
                case ".svg":
                    var svg = SvgSource.Load(FullPath);
                    Image = new SvgImage
                    {
                        Source = svg
                    };
                    break;
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                    Image = new Bitmap(FullPath);
                    break;
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            LoadingFailed = true;
        }

        IsLoading = false;
    }
}