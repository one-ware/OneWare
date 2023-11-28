using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.SDK.Enums;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;
using OneWare.SourceControl.ViewModels;
using OneWare.SourceControl.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.SourceControl;

public class SourceControlModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<CompareFileViewModel>();
        containerRegistry.RegisterSingleton<SourceControlViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var settingsService = containerProvider.Resolve<ISettingsService>();
        var windowService = containerProvider.Resolve<IWindowService>();
        
        settingsService.RegisterSettingCategory("Team Explorer", 10, "VsImageLib.Team16X");
        settingsService.RegisterTitled("Team Explorer", "Fetch", "SourceControl_AutoFetchEnable", 
            "Auto fetch", "Fetch for changed automatically", true);
        settingsService.RegisterTitledCombo("Team Explorer", "Fetch", "SourceControl_AutoFetchDelay", 
            "Auto fetch interval", "Interval in seconds", 60, 5, 10, 15, 30, 60);
        
        var dockService = containerProvider.Resolve<IDockService>();
        dockService.RegisterLayoutExtension<SourceControlViewModel>(DockShowLocation.Left);
        
        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("SourceControl")
        {
            Header = "Source Control",
            Command = new RelayCommand(() => dockService.Show(containerProvider.Resolve<SourceControlViewModel>())),
            ImageIconObservable = Application.Current?.GetResourceObservable(SourceControlViewModel.IconKey) ,
        });
        
        if (containerProvider.Resolve<SourceControlViewModel>() is not { } vm) return;

        windowService.RegisterUiExtension("MainWindow_BottomRightExtension", new SourceControlMainWindowBottomRightExtension()
        {
            DataContext = vm
        });
    }
}