using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SerialMonitor.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

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

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("SerialMonitor")
        {
            Header = "Serial Monitor",
            Command = new RelayCommand(() => dockService.Show(containerProvider.Resolve<ISerialMonitorService>())),
            IconObservable = Application.Current!.GetResourceObservable(SerialMonitorViewModel.IconKey)
        });
    }
}