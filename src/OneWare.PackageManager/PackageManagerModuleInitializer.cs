// OneWare.PackageManager/PackageManagerModuleInitializer.cs
using System.Collections.Generic; // For ListBoxSetting
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Models; // For MenuItemViewModel, ListBoxSetting
using OneWare.Essentials.Services; // For IWindowService, ISettingsService
using OneWare.Essentials.ViewModels; // For MenuItemViewModel, ListBoxSetting
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views; // For PackageManagerView
using System; // For ArgumentNullException
using System.Collections.ObjectModel; // For ObservableCollection used in ListBoxSetting

namespace OneWare.PackageManager
{
    public class PackageManagerModuleInitializer
    {
        private readonly IWindowService _windowService;
        private readonly ISettingsService _settingsService;
        private readonly PackageManagerViewModel _packageManagerViewModel; // Directly inject the concrete ViewModel

        // Constructor with all required dependencies
        public PackageManagerModuleInitializer(
            IWindowService windowService,
            ISettingsService settingsService,
            PackageManagerViewModel packageManagerViewModel) // Injected directly
        {
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _packageManagerViewModel = packageManagerViewModel ?? throw new ArgumentNullException(nameof(packageManagerViewModel));
        }

        public void Initialize()
        {
            _windowService.RegisterMenuItem("MainWindow_MainMenu/Extras", new MenuItemViewModel("Extensions")
            {
                Header = "Extensions",
                // The Command now directly references the injected ViewModel
                Command = new RelayCommand(() => _windowService.Show(new PackageManagerView
                {
                    DataContext = _packageManagerViewModel // Use the injected ViewModel
                })),
                IconObservable = Application.Current!.GetResourceObservable("PackageManager")
            });

            _settingsService.RegisterSettingCategory("Package Manager", 0, "PackageManager");

            _settingsService.RegisterSetting("Package Manager", "Sources", "PackageManager_Sources",
                new ListBoxSetting("Custom Package Sources", new ObservableCollection<string>()) // Use ObservableCollection for ListBoxSetting default
                {
                    MarkdownDocumentation = @"
                        Add custom package sources to the package manager. These sources will be used to search for and install packages.
                        You can add either:
                        - A Package Repository
                        - A Direct link to a package manifest
                    ",
                });
        }
    }
}