using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SerialMonitor.ViewModels;

namespace OneWare.SerialMonitor;

public class SerialMonitorModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<SerialMonitorViewModel>();
        services.AddSingleton<ISerialMonitorService>(provider => provider.Resolve<SerialMonitorViewModel>());
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var windowService = serviceProvider.Resolve<IWindowService>();
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();

        settingsService.Register("SerialMonitor_SelectedBaudRate", 9600);
        settingsService.Register("SerialMonitor_SelectedLineEncoding", "ASCII");
        settingsService.Register("SerialMonitor_SelectedLineEnding", @"\r\n");

        dockService.RegisterLayoutExtension<ISerialMonitorService>(DockShowLocation.Bottom);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("SerialMonitor")
        {
            Header = "Serial Monitor",
            Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<ISerialMonitorService>())),
            IconObservable = Application.Current!.GetResourceObservable(SerialMonitorViewModel.IconKey)
        });
    }
}