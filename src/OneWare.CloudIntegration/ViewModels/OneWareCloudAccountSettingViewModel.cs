using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using GitCredentialManager;
using OneWare.CloudIntegration.Settings;
using OneWare.Essentials.Services;
using OneWare.SourceControl.Views;

namespace OneWare.CloudIntegration.ViewModels;

public class OneWareCloudAccountSettingViewModel : ObservableObject
{
    private readonly IWindowService _windowService;
    private readonly ISettingsService _settingsService;
    private readonly IPaths _paths;
    private readonly Func<AuthenticateCloudViewModel> _viewModelFactory;

    public OneWareCloudAccountSetting Setting { get; }

    public OneWareCloudAccountSettingViewModel(
        OneWareCloudAccountSetting setting,
        IWindowService windowService,
        ISettingsService settingsService,
        IPaths paths,
        Func<AuthenticateCloudViewModel> viewModelFactory) // factory für neues ViewModel bei jedem Dialog
    {
        Setting = setting;
        _windowService = windowService;
        _settingsService = settingsService;
        _paths = paths;
        _viewModelFactory = viewModelFactory;
    }

    public Task LoginAsync(Control owner)
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
            _windowService.ShowDialogAsync(new AuthenticateCloudView()
            {
                DataContext = _viewModelFactory()
            }, TopLevel.GetTopLevel(owner) as Window));
    }

    public void Logout()
    {
        var store = CredentialManager.Create("oneware");
        store.Remove("https://one-ware.com", Setting.Value.ToString());
        Setting.Value = string.Empty;

        _settingsService.Save(_paths.SettingsPath);
    }
}
