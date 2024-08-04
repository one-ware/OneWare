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
using OneWare.UniversalFpgaProjectSystem.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class GenericExtensionViewModel : ExtensionViewModelBase
{
    private readonly string _guiPath;

    private readonly IDisposable? _fileWatcher;

    private bool _isLoading;
    
    private HardwareGuiViewModel? _guiViewModel;

    public GenericExtensionViewModel(FpgaExtensionModel extensionModel, string guiPath) : base(extensionModel)
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

    public HardwareGuiViewModel? GuiViewModel
    {
        get => _guiViewModel;
        set => SetProperty(ref _guiViewModel, value);
    }

    private async Task LoadGuiAsync()
    {
        IsLoading = true;

        GuiViewModel = await HardwareGuiCreator.CreateGuiAsync(_guiPath, ExtensionModel);
        
        IsLoading = false;
    }

    public override void Dispose()
    {
        _fileWatcher?.Dispose();
        base.Dispose();
    }
}