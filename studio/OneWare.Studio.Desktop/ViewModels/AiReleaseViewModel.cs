using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views;

namespace OneWare.Studio.Desktop.ViewModels;

public class AiReleaseViewModel : ObservableObject
{
    public const string ShowReleaseNotificationKey = "OneAI_ShowReleaseNotification";
    private const string ExtensionId = "OneWare.AI";
    
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly PackageManagerViewModel _packageManagerVm;
    private readonly PackageModel _aiPackage;
    private readonly IPaths _paths;
    private bool _hideNextTime;
    private bool _isLoading;
    
    public AiReleaseViewModel(IPaths paths, ISettingsService settingsService, 
        IWindowService windowService, PackageManagerViewModel packageManagerVm)
    {
        _paths = paths;
        _windowService = windowService;
        _settingsService = settingsService;
        _packageManagerVm = packageManagerVm;
        
        
    }

    public bool HideNextTime
    {
        get =>  _hideNextTime;
        set => SetProperty(ref _hideNextTime, value);
    }
    public bool IsLoading
    {
        get =>  _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool ExtensionIsAlreadyInstalled(IPackageService packageService)
    {
        return packageService.Packages[ExtensionId].Status == PackageStatus.Installed;
    }
    public async Task InstallPluginAsync(Control control)
    {
        if (IsLoading)
            return;
        
        try
        {
            Close(control);
            if (await _packageManagerVm.ShowSpecificPluginAsync("Plugins", ExtensionId) is { } pvm)
            {
                var view = new PackageManagerView
                {
                    DataContext = _packageManagerVm
                };
                _windowService.Show(view);
                await pvm.InstallCommand.ExecuteAsync(view);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
    public void Close(Control control)
    {
        if (IsLoading)
            return;
        
        if (HideNextTime)
        {
            _settingsService.SetSettingValue(ShowReleaseNotificationKey, !HideNextTime);
            _settingsService.Save(_paths.SettingsPath);
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(control);
        if (topLevel is Window wd)
        {
            wd.Close();
        }
    }
}