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
    private readonly ILogger<ImageViewModel> _logger;

    public ImageViewModel(string fullPath, 
        IProjectExplorerService projectExplorerService,
        ILogger<ImageViewModel> logger,
        IDockService dockService,
        IWindowService windowService) :
        base(fullPath, projectExplorerService, dockService, windowService)
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
                case ".jpeg":
                case ".png":
                    Image = new Bitmap(FullPath);
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message, e);
            LoadingFailed = true;
        }

        IsLoading = false;
    }
}