using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.PackageManager.ViewModels;

namespace OneWare.Studio.Desktop.ViewModels;

public class AiReleaseViewModel : ObservableObject
{
    public const string ShowReleaseNotificationKey = "OneAI_ShowReleaseNotification";
    private const string ExtensionId = "OneWare.AI";
    
    private readonly ISettingsService _settingsService;
    private readonly IPackageService _packageService;
    private readonly PackageModel _aiPackage;
    private readonly IPaths _paths;
    private bool _hideNextTime;
    private bool _isLoading;
    
    public AiReleaseViewModel(IPaths paths, ISettingsService settingsService, IPackageService packageService)
    {
        _paths = paths;
        _settingsService = settingsService;
        _packageService = packageService;
        _aiPackage = packageService.Packages[ExtensionId];
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

    public bool ExtensionIsAlreadyInstalled()
    {
        return _packageService.Packages[ExtensionId].Status == PackageStatus.Installed;
    }
    public async Task InstallPluginAsync(Control control)
    {
        if (IsLoading)
            return;
        
        try
        {
            IsLoading = true;
            await _packageService.InstallAsync(_aiPackage.Package);
        }
        finally
        {
            IsLoading = false;
        }
        Close(control);
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