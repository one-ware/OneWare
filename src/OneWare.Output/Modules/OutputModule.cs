using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Interfaces;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Output.ViewModels;

namespace OneWare.Output.Modules
{
    public class OutputModule(IContainerAdapter containerAdapter) : IOneWareModule
    {

        public const string KeyErrorListFilterMode = "ErrorList_FilterMode";
        public const string KeyErrorListShowExternalErrors = "ErrorList_ShowExternalErrors";
        public const string KeyErrorListVisibleSource = "ErrorList_VisibleSource";

        private readonly IContainerAdapter _containerAdapter = containerAdapter;
        private IDockService? _dockService; // Removed readonly modifier
        private IWindowService? _windowService; // Removed readonly modifier
        private ISettingsService? _settingsService;

        public void RegisterTypes()
        {
            _dockService = _containerAdapter.Resolve<IDockService>();
            _windowService = _containerAdapter.Resolve<IWindowService>();
            _settingsService = _containerAdapter.Resolve<ISettingsService>();


            // 1. Register OutputViewModel as IOutputService (singleton)
            _containerAdapter.Register<IOutputService, OutputViewModel>(isSingleton: true);

            // 2. Register OutputViewModel as OutputViewModel (singleton - self-registration)
            _containerAdapter.Register<OutputViewModel, OutputViewModel>(isSingleton: true);

            OnExecute();
        }

        public void OnExecute()
        {
            _dockService.RegisterLayoutExtension<IOutputService>(DockShowLocation.Bottom);

            _settingsService.Register("Output_Autoscroll", true);

            _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Output")
            {
                Header = "Output",
                Command = new RelayCommand(() => _dockService.Show(_containerAdapter.Resolve<IOutputService>())),
                IconObservable = Application.Current!.GetResourceObservable(OutputViewModel.IconKey)
            });
        }
    }
}