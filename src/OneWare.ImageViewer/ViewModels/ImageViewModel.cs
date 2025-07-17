using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;

namespace OneWare.ImageViewer.ViewModels;

public class ImageViewModel : ExtendedDocument
{
    private IImage? _image;

    public ImageViewModel(string fullPath, IProjectExplorerService projectExplorerService, IDockService dockService,
        IWindowService windowService) :
        base(fullPath, projectExplorerService, dockService, windowService)
    {
    }

    public IImage? Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }

    protected override void UpdateCurrentFile(IFile? oldFile)
    {
        if (CurrentFile is null) throw new NullReferenceException(nameof(CurrentFile));

        try
        {
            switch (CurrentFile.Extension.ToLower())
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
                    Image = new Bitmap(FullPath);
                    break;
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogError(e, e.Message);
            LoadingFailed = true;
        }

        IsLoading = false;
    }
}