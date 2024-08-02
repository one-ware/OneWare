using System.Text.Json;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga.Gui;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class GenericFpgaViewModel : FpgaViewModelBase
{
    private FpgaGui? _fpgaGui;
    private readonly string _guiPath;

    private readonly IDisposable? _fileWatcher;
    
    public GenericFpgaViewModel(FpgaModel fpgaModel, string guiPath) : base(fpgaModel)
    {
        _guiPath = guiPath;

        _ = LoadGuiAsync();
        
        _fileWatcher = FileSystemWatcherHelper.WatchFile(guiPath, () => _ = LoadGuiAsync());
    }

    public FpgaGui? FpgaGui
    {
        get => _fpgaGui;
        set => SetProperty(ref _fpgaGui, value);
    }

    private async Task LoadGuiAsync()
    {
        Console.WriteLine("load");
        try
        {
            await using var stream = File.OpenRead(_guiPath);
            FpgaGui = await JsonSerializer.DeserializeAsync<FpgaGui>(stream, new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    public override void Dispose()
    {
        _fileWatcher?.Dispose();
        base.Dispose();
    }
}