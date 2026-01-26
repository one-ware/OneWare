using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using GitCredentialManager;
using OneWare.Essentials.Services;
using OneWare.SourceControl.LoginProviders;
using OneWare.SourceControl.Settings;
using OneWare.SourceControl.Views;

namespace OneWare.SourceControl.ViewModels;

public class GitHubAccountSettingViewModel : ObservableObject
{
    public GitHubAccountSettingViewModel(GitHubAccountSetting setting)
    {
        Setting = setting;
    }

    public GitHubAccountSetting Setting { get; }

    public Task LoginAsync(Control owner)
    {
        return Dispatcher.UIThread.InvokeAsync(() => ContainerLocator.Container.Resolve<IWindowService>()
            .ShowDialogAsync(new AuthenticateGitView
            {
                DataContext = new AuthenticateGitViewModel(ContainerLocator.Container.Resolve<GithubLoginProvider>())
            }, TopLevel.GetTopLevel(owner) as Window));
    }

    public void Logout()
    {
        var store = CredentialManager.Create("oneware");
        store.Remove("https://github.com", Setting.Value.ToString());
        Setting.Value = string.Empty;

        ContainerLocator.Container.Resolve<ISettingsService>()
            .Save(ContainerLocator.Container.Resolve<IPaths>().SettingsPath);
    }
}