using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.ProjectExplorer.Views;

namespace OneWare.ProjectExplorer
{
    public class ProjectExplorerModuleInitializer
    {
        private readonly ProjectExplorerViewModel _projectExplorerViewModel; // Directly inject the concrete ViewModel
        private readonly IDockService _dockService;
        private readonly IWindowService _windowService;

        // Constructor with all required dependencies
        public ProjectExplorerModuleInitializer(
            ProjectExplorerViewModel projectExplorerViewModel, // Injected directly
            IDockService dockService,
            IWindowService windowService)
        {
            _projectExplorerViewModel = projectExplorerViewModel ?? throw new ArgumentNullException(nameof(projectExplorerViewModel));
            _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        }

        public void Initialize()
        {
            // Use the injected ViewModel and services directly
            _dockService.RegisterLayoutExtension<IProjectExplorerService>(DockShowLocation.Left);

            _windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension", new UiExtension(x =>
                new ProjectExplorerMainWindowToolBarExtension
                {
                    DataContext = _projectExplorerViewModel // Use the injected ViewModel
                }));

            _windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemViewModel("File")
            {
                Priority = -10,
                Header = "File"
            });

            _windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
                new MenuItemViewModel("File")
                {
                    Header = "File",
                    Command = new RelayCommand(() => _ = _projectExplorerViewModel.OpenFileDialogAsync()),
                    IconObservable = Application.Current!.GetResourceObservable("VsImageLib.NewFileCollection16X")
                });

            _windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
                new MenuItemViewModel("File")
                {
                    Header = "File",
                    Command = new RelayCommand(() => _ = _projectExplorerViewModel.ImportFileDialogAsync()),
                    IconObservable = Application.Current!.GetResourceObservable("VsImageLib.NewFileCollection16X")
                });

            _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
                new MenuItemViewModel("Project Explorer")
                {
                    Header = "Project Explorer",
                    Command =
                        new RelayCommand(() => _dockService.Show(_projectExplorerViewModel)), // Use the injected ViewModel
                    IconObservable = Application.Current!.GetResourceObservable(ProjectExplorerViewModel.IconKey)
                });
        }
    }
}
