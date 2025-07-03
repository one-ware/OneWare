using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.ErrorList.ViewModels;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Interfaces;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ErrorList.Modules
{
    public class ErrorListModule(IContainerAdapter containerAdapter) : IOneWareModule
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

            _containerAdapter.Register<IErrorService, ErrorListViewModel>(isSingleton: true);

            // 2. Register ErrorListViewModel as ErrorListViewModel (singleton - self-registration)
            _containerAdapter.Register<ErrorListViewModel, ErrorListViewModel>(isSingleton: true);

            
        }

        public void OnExecute()
        {
            _dockService.RegisterLayoutExtension<IErrorService>(DockShowLocation.Bottom);

            _settingsService.Register(KeyErrorListFilterMode, 0);
            _settingsService.RegisterTitled("Experimental", "Errors", KeyErrorListShowExternalErrors,
                "Show external errors", "Sets if errors from files outside of your project should be visible", false);
            _settingsService.Register(KeyErrorListVisibleSource, 0);

            _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Problems")
            {
                Header = "Problems",
                Command = new RelayCommand(() => _dockService.Show(_containerAdapter.Resolve<IErrorService>())),
                IconObservable = Application.Current!.GetResourceObservable(ErrorListViewModel.IconKey)
            });
        }
    }
}