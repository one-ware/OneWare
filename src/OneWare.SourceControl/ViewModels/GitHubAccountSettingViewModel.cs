using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using GitCredentialManager;
using OneWare.Essentials.Services;
using OneWare.Settings;
using OneWare.SourceControl.LoginProviders;
using OneWare.SourceControl.Settings;
using OneWare.SourceControl.Views;

namespace OneWare.SourceControl.ViewModels;

public class GitHubAccountSettingViewModel : ObservableObject
{
    private readonly IWindowService _windowService;
    private readonly GithubLoginProvider _githubLoginProvider;
    private readonly ISettingsService _settingsService;
    private readonly IPaths _paths;

    public GitHubAccountSetting Setting { get; }

    public GitHubAccountSettingViewModel(
        GitHubAccountSetting setting,
        IWindowService windowService,
        GithubLoginProvider githubLoginProvider,
        ISettingsService settingsService,
        IPaths paths)
    {
        Setting = setting;
        _windowService = windowService;
        _githubLoginProvider = githubLoginProvider;
        _settingsService = settingsService;
        _paths = paths;
    }

    public Task LoginAsync(Control owner)
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
            _windowService.ShowDialogAsync(
                new AuthenticateGitView
                {
                    DataContext = new AuthenticateGitViewModel(_githubLoginProvider)
                },
                TopLevel.GetTopLevel(owner) as Window
            )
        );
    }

    public void Logout()
    {
        var store = CredentialManager.Create("oneware");
        store.Remove("https://github.com", Setting.Value.ToString());
        Setting.Value = string.Empty;

        _settingsService.Save(_paths.SettingsPath);
    }
}
