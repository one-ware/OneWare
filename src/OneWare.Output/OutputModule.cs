using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using OneWare.Output.ViewModels;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.Shared.Enums;
using OneWare.Shared.Models;
using OneWare.Shared.Services;

namespace OneWare.Output;

public class OutputModule : IModule
{
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly IDockService _dockService;
    
    public OutputModule(ISettingsService settingsService, IDockService dockService, IWindowService windowService)
    {
        _settingsService = settingsService;
        _windowService = windowService;
        _dockService = dockService;
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterManySingleton<OutputViewModel>(typeof(IOutputService),
            typeof(OutputViewModel));
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        _dockService.RegisterLayoutExtension<IOutputService>(DockShowLocation.Bottom);

        _settingsService.Register("Output_Autoscroll", true);
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel()
        {
            Header = "Output",
            Command = new RelayCommand(() => _dockService.Show(containerProvider.Resolve<IOutputService>())),
            Icon = Application.Current?.FindResource("BoxIcons.RegularCode") as IImage,
        });
    }
}