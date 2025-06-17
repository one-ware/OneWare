using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using GitCredentialManager;
using OneWare.CloudIntegration.Settings;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.SourceControl.Views;

namespace OneWare.CloudIntegration.ViewModels;

public class OneWareCloudAccountSettingViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly IWindowService _windowService;
    private readonly IPaths _paths;
    private readonly AuthenticateCloudViewModel _authenticateCloudViewModel;
    public OneWareCloudAccountSetting Setting { get; set; }


    public OneWareCloudAccountSettingViewModel( ISettingsService settingsService, ILogger logger, IWindowService windowService, IPaths paths, AuthenticateCloudViewModel authenticateCloudViewModel)
    {
        
        _settingsService = settingsService;
        _logger = logger;
        _windowService = windowService;
        _paths = paths;
        _authenticateCloudViewModel = authenticateCloudViewModel;
    }

    public Task LoginAsync(Control owner)
    {
        return Dispatcher.UIThread.InvokeAsync(() => _windowService
            .ShowDialogAsync(new AuthenticateCloudView()
            {
                DataContext = _authenticateCloudViewModel
            }, TopLevel.GetTopLevel(owner) as Window));
    }

    public void Logout()
    {
        var store = CredentialManager.Create("oneware");
        store.Remove("https://one-ware.com", Setting.Value.ToString());
        Setting.Value = string.Empty;

        _settingsService
            .Save(_paths.SettingsPath); 
    }

   
}