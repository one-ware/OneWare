using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using OneWare.Terminal.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

using OneWare.Shared.Enums;
using OneWare.Shared.Models;
using OneWare.Shared.Services;

namespace OneWare.Terminal;

public class TerminalModule : IModule
{
    private readonly IWindowService _windowService;
    private readonly IDockService _dockService;
    public TerminalModule(IWindowService windowService, IDockService dockService)
    {
        _windowService = windowService;
        _dockService = dockService;
        
        _dockService.RegisterLayoutExtension<TerminalViewModel>(DockShowLocation.Bottom);
    }
    
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<TerminalViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel()
        {
            Header = "Terminal",
            Command = new RelayCommand(() => _dockService.Show(containerProvider.Resolve<TerminalViewModel>())),
            Icon = Application.Current?.FindResource("BoxIcons.RegularCode") as IImage,
        });
    }
}