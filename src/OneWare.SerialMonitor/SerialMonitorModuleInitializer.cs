using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SerialMonitor.ViewModels;

namespace OneWare.SerialMonitor
{
    public class SerialMonitorModuleInitializer
    {
        private readonly IWindowService _windowService;
        private readonly IDockService _dockService;
        private readonly ISettingsService _settingsService;
        private readonly ISerialMonitorService _serialMonitorService; // Inject the service directly

        // Constructor with all required dependencies
        public SerialMonitorModuleInitializer(
            IWindowService windowService,
            IDockService dockService,
            ISettingsService settingsService,
            ISerialMonitorService serialMonitorService) // Directly inject the service
        {
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _serialMonitorService = serialMonitorService ?? throw new ArgumentNullException(nameof(serialMonitorService));
        }

        public void Initialize()
        {
            // Register settings
            _settingsService.Register("SerialMonitor_SelectedBaudRate", 9600);
            _settingsService.Register("SerialMonitor_SelectedLineEncoding", "ASCII");
            _settingsService.Register("SerialMonitor_SelectedLineEnding", @"\r\n");

            // Register layout extension
            _dockService.RegisterLayoutExtension<ISerialMonitorService>(DockShowLocation.Bottom);

            // Register menu item
            _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("SerialMonitor")
            {
                Header = "Serial Monitor",
                // Use the injected _serialMonitorService directly
                Command = new RelayCommand(() => _dockService.Show(_serialMonitorService)),
                IconObservable = Application.Current!.GetResourceObservable(SerialMonitorViewModel.IconKey)
            });
        }
    }
}
