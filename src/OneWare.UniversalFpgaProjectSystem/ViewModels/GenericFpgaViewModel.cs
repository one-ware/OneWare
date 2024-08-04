using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class GenericFpgaViewModel : FpgaViewModelBase
{
    private readonly string _guiPath;

    private readonly IDisposable? _fileWatcher;

    private bool _isLoading;

    private int _width;

    private int _height;

    private IImage? _image;

    public GenericFpgaViewModel(FpgaModel fpgaModel, string guiPath) : base(fpgaModel)
    {
        _guiPath = guiPath;

        _ = LoadGuiAsync();

        _fileWatcher =
            FileSystemWatcherHelper.WatchFile(guiPath, () => Dispatcher.UIThread.Post(() => _ = LoadGuiAsync()));
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public int Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    public int Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    public IImage? Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }

    public ObservableCollection<FpgaGuiElementViewModelBase> Elements { get; } = new();

    private async Task LoadGuiAsync()
    {
        IsLoading = true;
        Width = 0;
        Height = 0;
        Image = null;
        Elements.Clear();

        try
        {
            await using var stream = File.OpenRead(_guiPath);
            using var document = await JsonDocument.ParseAsync(stream);
            var gui = document.RootElement;

            Width = gui.GetProperty("width").GetInt32();
            Height = gui.GetProperty("height").GetInt32();

            if (gui.TryGetProperty("image", out var imageProperty) && imageProperty.GetString() is { } image)
            {
                var fullPath = Path.Combine(Path.GetDirectoryName(_guiPath)!, image);
                switch (Path.GetExtension(fullPath).ToLower())
                {
                    case ".svg":
                        var svg = SvgSource.Load(fullPath);
                        Image = new SvgImage
                        {
                            Source = svg
                        };
                        break;
                    case ".jpg":
                    case ".png":
                        Image = new Bitmap(fullPath);
                        break;
                }
            }

            foreach (var element in gui.GetProperty("elements").EnumerateArray())
            {
                var x = element.GetProperty("x").GetInt32();
                var y = element.GetProperty("y").GetInt32();
                var width = element.TryGetProperty("width", out var widthProperty)
                    ? widthProperty.GetInt32()
                    : 0;
                var height = element.TryGetProperty("height", out var heightProperty)
                    ? heightProperty.GetInt32()
                    : 0;
                var rotation = element.TryGetProperty("rotation", out var rotationProperty)
                    ? rotationProperty.GetDouble()
                    : 0;
                var color = element.TryGetProperty("color", out var colorProperty)
                    ? new BrushConverter().ConvertFromString(colorProperty.GetString() ?? string.Empty) as IBrush
                    : null;

                switch (element.GetProperty("type").GetString())
                {
                    case "ellipse":
                    {
                        Elements.Add(new FpgaGuiElementEllipseViewModel(x, y, width, height, color!)
                        {
                            Rotation = rotation,
                        });
                        break;
                    }
                    case "rect":
                    {
                        Elements.Add(new FpgaGuiElementRectViewModel(x, y, width, height, color!)
                        {
                            Rotation = rotation,
                            CornerRadius = element.TryGetProperty("cornerRadius", out var cornerRadiusProperty)
                                ? CornerRadius.Parse(cornerRadiusProperty.GetString()!)
                                : default,
                            BoxShadow = element.TryGetProperty("boxShadow", out var boxShadowProperty)
                                ? BoxShadows.Parse(boxShadowProperty.GetString()!)
                                : default
                        });
                        break;
                    }
                    case "pin":
                    {
                        element.TryGetProperty("bind", out var bindProperty);
                        FpgaModel.PinModels.TryGetValue(bindProperty.GetString() ?? string.Empty, out var pinModel);

                        color ??= Brushes.YellowGreen;

                        Elements.Add(new FpgaGuiElementPinViewModel(x, y, width, height, color!)
                        {
                            Rotation = rotation,
                            PinModel = pinModel,
                        });
                        break;
                    }
                    case "text":
                    {
                        var fontWeightStr = element.TryGetProperty("fontWeight", out var fontWeightProperty)
                            ? fontWeightProperty.GetString()
                            : null;

                        if (!Enum.TryParse<FontWeight>(fontWeightStr ?? "Normal", true, out var fontWeight))
                        {
                            fontWeight = FontWeight.Normal;
                        }

                        var text = element.GetProperty("text").GetString();

                        var fontSize = element.TryGetProperty("fontSize", out var fontSizeProperty)
                            ? fontSizeProperty.GetInt32()
                            : 12;

                        Elements.Add(new FpgaGuiElementTextViewModel(x, y, text!)
                        {
                            Rotation = rotation,
                            Color = color,
                            FontWeight = fontWeight,
                            FontSize = fontSize
                        });
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }

        IsLoading = false;
    }

    public override void Dispose()
    {
        _fileWatcher?.Dispose();
        base.Dispose();
    }
}