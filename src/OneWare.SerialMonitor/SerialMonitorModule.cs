using Autofac;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SerialMonitor.ViewModels;

namespace OneWare.SerialMonitor;

public class SerialMonitorModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Register SerialMonitorViewModel as both itself and ISerialMonitorService
        builder.RegisterType<SerialMonitorViewModel>()
            .As<ISerialMonitorService>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterBuildCallback(container =>
        {
            var windowService = container.Resolve<IWindowService>();
            var dockService = container.Resolve<IDockService>();
            var settingsService = container.Resolve<ISettingsService>();
            var serialMonitor = container.Resolve<ISerialMonitorService>();

            settingsService.Register("SerialMonitor_SelectedBaudRate", 9600);
            settingsService.Register("SerialMonitor_SelectedLineEncoding", "ASCII");
            settingsService.Register("SerialMonitor_SelectedLineEnding", @"\r\n");

            dockService.RegisterLayoutExtension<ISerialMonitorService>(DockShowLocation.Bottom);

            windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("SerialMonitor")
            {
                Header = "Serial Monitor",
                Command = new RelayCommand(() => dockService.Show(serialMonitor)),
                IconObservable = Application.Current!.GetResourceObservable(SerialMonitorViewModel.IconKey)
            });
        });
    }
}
