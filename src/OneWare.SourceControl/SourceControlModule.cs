using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SourceControl.ViewModels;
using OneWare.SourceControl.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.SourceControl;

public class SourceControlModule : IModule
{
    public const string GitHubAccountNameKey = "SourceControl_GitHub_AccountName";
    
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<CompareFileViewModel>();
        containerRegistry.RegisterSingleton<SourceControlViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var settingsService = containerProvider.Resolve<ISettingsService>();
        var windowService = containerProvider.Resolve<IWindowService>();

        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", "secretservice");
        
        settingsService.RegisterTitled("Team Explorer", "GitHub", GitHubAccountNameKey,
            "Account Name", "GitHub Account Name (gets set automatically when authenticating using a token)", "");
        
        settingsService.RegisterSettingCategory("Team Explorer", 10, "VsImageLib.Team16X");
        settingsService.RegisterTitled("Team Explorer", "Fetch", "SourceControl_AutoFetchEnable",
            "Auto fetch", "Fetch for changed automatically", true);
        settingsService.RegisterTitledSlider("Team Explorer", "Fetch", "SourceControl_AutoFetchDelay",
            "Auto fetch interval", "Interval in seconds", 60, 5, 60, 5);
        settingsService.RegisterTitled("Team Explorer", "Polling", "SourceControl_PollChangesEnable",
            "Poll for changes", "Fetch for changed files automatically", true);
        settingsService.RegisterTitledSlider("Team Explorer", "Polling", "SourceControl_PollChangesDelay",
            "Poll changes interval", "Interval in seconds", 5, 1, 60, 1);
        

        var dockService = containerProvider.Resolve<IDockService>();
        dockService.RegisterLayoutExtension<SourceControlViewModel>(DockShowLocation.Left);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("SourceControl")
        {
            Header = "Source Control",
            Command = new RelayCommand(() => dockService.Show(containerProvider.Resolve<SourceControlViewModel>())),
            IconObservable = Application.Current!.GetResourceObservable(SourceControlViewModel.IconKey)
        });

        if (containerProvider.Resolve<SourceControlViewModel>() is not { } vm) return;

        windowService.RegisterUiExtension("MainWindow_BottomRightExtension", new UiExtension(x =>
            new SourceControlMainWindowBottomRightExtension
            {
                DataContext = vm
            }));
    }
}