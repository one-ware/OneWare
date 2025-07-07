using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Interfaces;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.LibraryExplorer.ViewModels;

namespace OneWare.LibraryExplorer.Modules
{
    public class LibraryExplorerModule(IContainerAdapter containerAdapter) : IOneWareModule
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

            _containerAdapter.Register<LibraryExplorerViewModel, LibraryExplorerViewModel>();

            OnExecute();
        }

        public void OnExecute()
        {
            _dockService.RegisterLayoutExtension<LibraryExplorerViewModel>(DockShowLocation.Left);

            _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
                new MenuItemViewModel("Library Explorer")
                {
                    Header = "Library Explorer",
                    Command =
                        new RelayCommand(() => _dockService.Show(_containerAdapter.Resolve<LibraryExplorerViewModel>())),
               //     IconObservable = Application.Current!.GetResourceObservable(LibraryExplorerViewModel.IconKey)
                });
        }
    }
}