using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SearchList.ViewModels;

namespace OneWare.SearchList
{
    public class SearchListModuleInitializer
    {
        private readonly IDockService _dockService;
        private readonly IWindowService _windowService;
        private readonly SearchListViewModel _searchListViewModel; // Directly inject the concrete ViewModel
        private readonly PlatformHelper _platformHelper;

        // Constructor with all required dependencies
        public SearchListModuleInitializer(
            IWindowService windowService,
            PlatformHelper platformHelper,
            IDockService dockService,
            SearchListViewModel searchListViewModel) // Injected directly
        {
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
            _searchListViewModel = searchListViewModel ?? throw new ArgumentNullException(nameof(searchListViewModel));
            _platformHelper = platformHelper ?? throw new ArgumentNullException(nameof(platformHelper));
        }

        public void Initialize()
        {
            _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Search")
            {
                Header = "Search",
                Command = new RelayCommand(() =>
                {
                    _searchListViewModel.SearchString = string.Empty; // Use the injected ViewModel
                    _dockService.Show(_searchListViewModel); // Use the injected ViewModel
                }),
                IconObservable = Application.Current!.GetResourceObservable(SearchListViewModel.IconKey),
                InputGesture = new KeyGesture(Key.F, KeyModifiers.Shift | _platformHelper.ControlKey)
            });
        }
    }
}
