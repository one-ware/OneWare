using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.SDK.Enums;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;
using OneWare.TerminalManager.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.TerminalManager;

public class TerminalManagerModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<TerminalManagerViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IDockService>().RegisterLayoutExtension<TerminalManagerViewModel>(DockShowLocation.Bottom);
        containerProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Terminal")
        {
            Header = "Terminal",
            Command = new RelayCommand(() => containerProvider.Resolve<IDockService>().Show(containerProvider.Resolve<TerminalManagerViewModel>())),
            ImageIconObservable = Application.Current?.GetResourceObservable(TerminalManagerViewModel.IconKey),
        });
    }
}