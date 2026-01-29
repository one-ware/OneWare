using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;
using OneWare.Essentials.Services;
using AuthenticateCloudView = OneWare.CloudIntegration.Views.AuthenticateCloudView;

namespace OneWare.CloudIntegration.ViewModels;

public class OneWareCloudAccountSettingViewModel : ObservableObject
{
    public OneWareCloudAccountSettingViewModel(OneWareCloudAccountSetting setting)
    {
        Setting = setting;
    }

    public OneWareCloudAccountSetting Setting { get; }

    public Task LoginAsync(Control owner)
    {
        return Dispatcher.UIThread.InvokeAsync(() => ContainerLocator.Container.Resolve<IWindowService>()
            .ShowDialogAsync(new AuthenticateCloudView
            {
                DataContext = ContainerLocator.Container.Resolve<AuthenticateCloudViewModel>()
            }, TopLevel.GetTopLevel(owner) as Window));
    }

    public void Logout()
    {
        ContainerLocator.Container.Resolve<OneWareCloudLoginService>().Logout(Setting.Value.ToString()!);

        Setting.Value = string.Empty;

        ContainerLocator.Container.Resolve<ISettingsService>()
            .Save(ContainerLocator.Container.Resolve<IPaths>().SettingsPath);
    }
}