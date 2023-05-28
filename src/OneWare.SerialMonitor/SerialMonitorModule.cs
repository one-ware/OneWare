using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using OneWare.SerialMonitor.ViewModels;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.Output;
using OneWare.Shared.Enums;
using OneWare.Shared.Models;
using OneWare.Shared.Services;

namespace OneWare.SerialMonitor;

public class SerialMonitorModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterManySingleton<SerialMonitorViewModel>(typeof(ISerialMonitorService),
            typeof(SerialMonitorViewModel));
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var windowService = containerProvider.Resolve<IWindowService>();
        var dockService = containerProvider.Resolve<IDockService>();
        var settingsService = containerProvider.Resolve<ISettingsService>();
        
        settingsService.Register("SerialMonitor_SelectedBaudRate", 9600);
        settingsService.Register("SerialMonitor_SelectedLineEncoding", "ASCII");
        settingsService.Register("SerialMonitor_SelectedLineEnding", @"\r\n");
        
        dockService.RegisterLayoutExtension<ISerialMonitorService>(DockShowLocation.Bottom);
        
        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel()
        {
            Header = "Serial Monitor",
            Command = new RelayCommand(() => dockService.Show(containerProvider.Resolve<ISerialMonitorService>())),
            Icon = Application.Current?.FindResource("BoxIcons.RegularCode") as IImage,
        });
    }
}