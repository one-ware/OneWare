using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Output.ViewModels;
using Autofac;

namespace OneWare.Output
{
    public class OutputModule
    {
        private readonly IDockService _dockService;
        private readonly ISettingsService _settingsService;
        private readonly IWindowService _windowService;

        public OutputModule(IDockService dockService, ISettingsService settingsService, IWindowService windowService)
        {
            _dockService = dockService;
            _settingsService = settingsService;
            _windowService = windowService;
        }

        public void RegisterTypes(ContainerBuilder builder)
        {
            // Register OutputViewModel and IOutputService as singletons
            builder.RegisterType<OutputViewModel>().As<IOutputService>().SingleInstance();
            builder.RegisterType<OutputViewModel>().AsSelf().SingleInstance();
        }

        public void OnInitialized(IComponentContext context)
        {
            // Register the DockService layout extension
            _dockService.RegisterLayoutExtension<IOutputService>(DockShowLocation.Bottom);

            // Register settings
            _settingsService.Register("Output_Autoscroll", true);

            // Register the menu item for the output window
            _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Output")
            {
                Header = "Output",
                Command = new RelayCommand(() => _dockService.Show(context.Resolve<IOutputService>())),
                IconObservable = Application.Current!.GetResourceObservable(OutputViewModel.IconKey)
            });
        }
    }
}
