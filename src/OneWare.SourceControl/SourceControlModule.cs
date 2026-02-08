using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SourceControl.Settings;
using OneWare.SourceControl.ViewModels;
using OneWare.SourceControl.Views;

namespace OneWare.SourceControl;

public class SourceControlModule : OneWareModuleBase
{
    public const string GitHubAccountNameKey = "SourceControl_GitHub_AccountName";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<CompareGitViewModel>();
        services.AddSingleton<SourceControlViewModel>();
        services.AddSingleton<GitHubAccountSettingViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var windowService = serviceProvider.Resolve<IWindowService>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", "secretservice");

        settingsService.RegisterCustom("Team Explorer", "GitHub", GitHubAccountNameKey, new GitHubAccountSetting());

        settingsService.RegisterSettingCategory("Team Explorer", 10, "VsImageLib.Team16X");
        settingsService.RegisterSetting("Team Explorer", "Fetch", "SourceControl_AutoFetchEnable",
            new CheckBoxSetting("Auto fetch", true)
            {
                HoverDescription = "Fetch for changed automatically"
            });
        settingsService.RegisterSetting("Team Explorer", "Fetch", "SourceControl_AutoFetchDelay",
            new SliderSetting("Auto fetch interval", 60, 5, 60, 5)
            {
                HoverDescription = "Interval in seconds"
            });
        settingsService.RegisterSetting("Team Explorer", "Polling", "SourceControl_PollChangesEnable",
            new CheckBoxSetting("Poll for changes", true)
            {
                HoverDescription = "Fetch for changed files automatically"
            });
        settingsService.RegisterSetting("Team Explorer", "Polling", "SourceControl_PollChangesDelay",
            new SliderSetting("Poll changes interval", 5, 1, 60, 1)
            {
                HoverDescription = "Interval in seconds"
            });

        var dockService = serviceProvider.Resolve<IMainDockService>();
        dockService.RegisterLayoutExtension<SourceControlViewModel>(DockShowLocation.Left);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("SourceControl")
        {
            Header = "Source Control",
            Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<SourceControlViewModel>())),
            IconModel = new IconModel(SourceControlViewModel.IconKey)
        });

        if (serviceProvider.Resolve<SourceControlViewModel>() is not { } vm) return;

        windowService.RegisterUiExtension("MainWindow_BottomRightExtension", new OneWareUiExtension(_ =>
            new SourceControlMainWindowBottomRightExtension
            {
                DataContext = vm
            }));
    }
}
