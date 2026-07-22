using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using BitMiracle.LibTiff.Classic;
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
                case ".tif":
                case ".tiff":
                    Image = LoadTiff(FullPath);
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

    private static Bitmap LoadTiff(string path)
    {
        using var tiff = Tiff.Open(path, "r")
                         ?? throw new InvalidDataException($"Unable to open TIFF image '{path}'.");

        var width = tiff.GetField(TiffTag.IMAGEWIDTH)?[0].ToInt() ?? 0;
        var height = tiff.GetField(TiffTag.IMAGELENGTH)?[0].ToInt() ?? 0;
        if (width <= 0 || height <= 0)
            throw new InvalidDataException($"TIFF image '{path}' has invalid dimensions.");

        var pixelCount = checked(width * height);
        var raster = new int[pixelCount];
        if (!tiff.ReadRGBAImageOriented(width, height, raster, Orientation.TOPLEFT))
            throw new InvalidDataException($"Unable to decode TIFF image '{path}'.");

        var pixels = new byte[checked(pixelCount * 4)];
        for (var i = 0; i < raster.Length; i++)
        {
            var offset = i * 4;
            pixels[offset] = (byte)Tiff.GetB(raster[i]);
            pixels[offset + 1] = (byte)Tiff.GetG(raster[i]);
            pixels[offset + 2] = (byte)Tiff.GetR(raster[i]);
            pixels[offset + 3] = (byte)Tiff.GetA(raster[i]);
        }

        var pixelsHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, pixelsHandle.AddrOfPinnedObject(),
                new PixelSize(width, height), new Vector(96, 96), checked(width * 4));
        }
        finally
        {
            pixelsHandle.Free();
        }
    }
}