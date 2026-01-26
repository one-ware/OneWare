using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Services;

namespace OneWare.Studio.Desktop.ViewModels;

public class AiReleaseWindowViewModel : ObservableObject
{
    public const string ShowReleaseNotificationKey = "OneAI_ShowReleaseNotification";
    private const string ExtensionId = "OneWare.AI";
    private readonly IPackageWindowService _packageWindowManager;
    private readonly IPaths _paths;

    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;
    private bool _hideNextTime;

    public AiReleaseWindowViewModel(IPaths paths, ISettingsService settingsService,
        IWindowService windowService, IPackageWindowService packageWindowManager)
    {
        _paths = paths;
        _windowService = windowService;
        _settingsService = settingsService;
        _packageWindowManager = packageWindowManager;
    }

    public bool HideNextTime
    {
        get => _hideNextTime;
        set => SetProperty(ref _hideNextTime, value);
    }

    public bool ExtensionIsAlreadyInstalled(IPluginService pluginService)
    {
        return pluginService.InstalledPlugins.FirstOrDefault(x => x.Id == ExtensionId) != null;
    }

    public async Task InstallPluginAsync(Control control)
    {
        Close(control);
        await _packageWindowManager.QuickInstallPackageAsync(ExtensionId);
    }

    public void Close(Control control)
    {
        if (HideNextTime)
        {
            _settingsService.SetSettingValue(ShowReleaseNotificationKey, !HideNextTime);
            _settingsService.Save(_paths.SettingsPath);
        }

        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel is Window wd) wd.Close();
    }
}