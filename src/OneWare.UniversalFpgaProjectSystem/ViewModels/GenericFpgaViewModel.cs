using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga.Gui;
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
        
        _fileWatcher = FileSystemWatcherHelper.WatchFile(guiPath, () => _ = LoadGuiAsync());
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
            var gui = await JsonSerializer.DeserializeAsync<FpgaGui>(stream, new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });

            if (gui == null)
            {
                return;
            }
            
            Width = gui.Width;
            Height = gui.Height;

            if (gui.Image != null)
            {
                var fullPath = Path.Combine(Path.GetDirectoryName(_guiPath)!, gui.Image);
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
            
            if (gui.Elements != null)
            {
                foreach (var element in gui.Elements)
                {
                    if (element.Type == "pin")
                    {
                        FpgaModel.PinModels.TryGetValue(element.Bind ?? string.Empty, out var pinModel);
                        
                        var color = element.Color != null ? new BrushConverter().ConvertFromString(element.Color ?? string.Empty) as IBrush : Brushes.YellowGreen;
                        
                        Elements.Add(new FpgaGuiElementPinViewModel(element.X, element.Y, element.Width, element.Height, pinModel, color!));
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