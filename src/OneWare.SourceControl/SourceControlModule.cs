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
        services.AddTransient<CompareFileViewModel>();
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
        settingsService.RegisterTitled("Team Explorer", "Fetch", "SourceControl_AutoFetchEnable",
            "Auto fetch", "Fetch for changed automatically", true);
        settingsService.RegisterTitledSlider("Team Explorer", "Fetch", "SourceControl_AutoFetchDelay",
            "Auto fetch interval", "Interval in seconds", 60, 5, 60, 5);
        settingsService.RegisterTitled("Team Explorer", "Polling", "SourceControl_PollChangesEnable",
            "Poll for changes", "Fetch for changed files automatically", true);
        settingsService.RegisterTitledSlider("Team Explorer", "Polling", "SourceControl_PollChangesDelay",
            "Poll changes interval", "Interval in seconds", 5, 1, 60, 1);

        var dockService = serviceProvider.Resolve<IMainDockService>();
        dockService.RegisterLayoutExtension<SourceControlViewModel>(DockShowLocation.Left);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("SourceControl")
        {
            Header = "Source Control",
            Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<SourceControlViewModel>())),
            IconObservable = Application.Current!.GetResourceObservable(SourceControlViewModel.IconKey)
        });

        if (serviceProvider.Resolve<SourceControlViewModel>() is not { } vm) return;

        windowService.RegisterUiExtension("MainWindow_BottomRightExtension", new OneWareUiExtension(_ =>
            new SourceControlMainWindowBottomRightExtension
            {
                DataContext = vm
            }));
    }
}

