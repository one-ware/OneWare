using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Shared.Enums;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
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
        containerProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemModel("Terminal")
        {
            Header = "Terminal",
            Command = new RelayCommand(() => containerProvider.Resolve<IDockService>().Show(containerProvider.Resolve<TerminalManagerViewModel>())),
            ImageIconObservable = Application.Current?.GetResourceObservable(TerminalManagerViewModel.IconKey),
        });
    }
}