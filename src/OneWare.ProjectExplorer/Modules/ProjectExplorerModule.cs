using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.ProjectExplorer.Services;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.ProjectExplorer.Views;


namespace OneWare.ProjectExplorer.Modules
{
    public class ProjectExplorerModule
    {
        public const string KeyErrorListFilterMode = "ErrorList_FilterMode";
        public const string KeyErrorListShowExternalErrors = "ErrorList_ShowExternalErrors";
        public const string KeyErrorListVisibleSource = "ErrorList_VisibleSource";

        private readonly IContainerAdapter _containerAdapter;
        private IDockService? _dockService; // Removed readonly modifier
        private IWindowService? _windowService; // Removed readonly modifier
        private ISettingsService? _settingsService;



        public ProjectExplorerModule(IContainerAdapter containerAdapter)
        {
            _containerAdapter = containerAdapter;
        }

        public void Load()
        {
            _dockService = _containerAdapter.Resolve<IDockService>();
            _windowService = _containerAdapter.Resolve<IWindowService>();
            _settingsService = _containerAdapter.Resolve<ISettingsService>();

            _containerAdapter.Register<IFileWatchService, FileWatchService>(isSingleton: true);

            _containerAdapter.Register<IProjectExplorerService, ProjectExplorerViewModel>(isSingleton: true);
            _containerAdapter.Register<ProjectExplorerViewModel, ProjectExplorerViewModel>(isSingleton: true);
            Register();
        }

        private void Register()
        {
            if (_containerAdapter.Resolve<IProjectExplorerService>() is not ProjectExplorerViewModel vm) return;

            var dockService = _containerAdapter.Resolve<IDockService>();
            var windowService = _containerAdapter.Resolve<IWindowService>();

            dockService.RegisterLayoutExtension<IProjectExplorerService>(DockShowLocation.Left);

            windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension", new UiExtension(x =>
                new ProjectExplorerMainWindowToolBarExtension
                {
                    DataContext = vm
                }));

            windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemViewModel("File")
            {
                Priority = -10,
                Header = "File"
            });

            windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
                new MenuItemViewModel("File")
                {
                    Header = "File",
                    Command = new RelayCommand(() => _ = vm.OpenFileDialogAsync()),
                    IconObservable = Application.Current!.GetResourceObservable("VsImageLib.NewFileCollection16X")
                });

            windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
                new MenuItemViewModel("File")
                {
                    Header = "File",
                    Command = new RelayCommand(() => _ = vm.ImportFileDialogAsync()),
                    IconObservable = Application.Current!.GetResourceObservable("VsImageLib.NewFileCollection16X")
                });

            windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
                new MenuItemViewModel("Project Explorer")
                {
                    Header = "Project Explorer",
                    Command =
                        new RelayCommand(() => dockService.Show(_containerAdapter.Resolve<IProjectExplorerService>())),
                    IconObservable = Application.Current!.GetResourceObservable(ProjectExplorerViewModel.IconKey)
                });
        }
    }
}