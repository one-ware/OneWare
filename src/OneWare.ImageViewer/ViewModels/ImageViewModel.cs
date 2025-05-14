using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Microsoft.Extensions.Logging;

namespace OneWare.ImageViewer.ViewModels;

public class ImageViewModel : ExtendedDocument
{
    private readonly ILogger<ImageViewModel> _logger;
    private IImage? _image;

    public ImageViewModel(
        string fullPath,
        IProjectExplorerService projectExplorerService,
        IDockService dockService,
        IWindowService windowService,
        ILogger<ImageViewModel> logger) : base(fullPath, projectExplorerService, dockService, windowService)
    {
        _logger = logger;
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
                case ".png":
                    Image = new Bitmap(FullPath);
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load image.");
            LoadingFailed = true;
        }

        IsLoading = false;
    }
}
