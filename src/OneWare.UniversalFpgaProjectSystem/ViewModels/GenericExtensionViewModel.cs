using Avalonia.Threading;
using OneWare.Essentials.Helpers;
using OneWare.UniversalFpgaProjectSystem.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class GenericExtensionViewModel : ExtensionViewModelBase
{
    private readonly IDisposable? _fileWatcher;
    private readonly string _guiPath;

    private HardwareGuiViewModel? _guiViewModel;

    private bool _isLoading;

    public GenericExtensionViewModel(ExtensionModel extensionModel, string guiPath) : base(extensionModel)
    {
        _guiPath = guiPath;

        _ = LoadGuiAsync();

        if (!guiPath.StartsWith("avares://"))
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

    public override void Initialize()
    {
        base.Initialize();

        GuiViewModel?.Initialize();
    }

    public override void Dispose()
    {
        _fileWatcher?.Dispose();
        base.Dispose();
    }
}