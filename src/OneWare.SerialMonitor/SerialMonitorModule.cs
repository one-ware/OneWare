using Autofac;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SerialMonitor.ViewModels;

namespace OneWare.SerialMonitor
{
    public class SerialMonitorModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Register types with Autofac
            builder.RegisterType<SerialMonitorViewModel>()
                   .As<ISerialMonitorService>()
                   .AsSelf()
                   .SingleInstance();

            base.Load(builder);
        }

        public void OnInitialized(IComponentContext context)
        {
            var windowService = context.Resolve<IWindowService>();
            var dockService = context.Resolve<IDockService>();
            var settingsService = context.Resolve<ISettingsService>();

            settingsService.Register("SerialMonitor_SelectedBaudRate", 9600);
            settingsService.Register("SerialMonitor_SelectedLineEncoding", "ASCII");
            settingsService.Register("SerialMonitor_SelectedLineEnding", @"\r\n");

            dockService.RegisterLayoutExtension<ISerialMonitorService>(DockShowLocation.Bottom);

            windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("SerialMonitor")
            {
                Header = "Serial Monitor",
                Command = new RelayCommand(() => dockService.Show(context.Resolve<ISerialMonitorService>())),
                IconObservable = Application.Current!.GetResourceObservable(SerialMonitorViewModel.IconKey)
            });
        }
    }
}
