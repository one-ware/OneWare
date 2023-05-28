using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.ProjectExplorer.Views;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.Shared.Enums;
using OneWare.Shared.Models;
using OneWare.Shared.Services;

namespace OneWare.ProjectExplorer;

public class ProjectExplorerModule : IModule
{
    private readonly IWindowService _windowService;
    private readonly IDockService _dockService;
    
    public ProjectExplorerModule(IWindowService windowService, IDockService dockService)
    {
        _windowService = windowService;
        _dockService = dockService;
    }
    
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterManySingleton<ProjectExplorerViewModel>(typeof(IProjectService),
            typeof(ProjectExplorerViewModel));
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        if (containerProvider.Resolve<IProjectService>() is not ProjectExplorerViewModel vm) return;
        
        _dockService.RegisterLayoutExtension<IProjectService>(DockShowLocation.Left);

        _windowService.RegisterUiExtension("MainWindow_RoundToolBar", new ProjectExplorerMainWindowToolBarExtension()
        {
            DataContext = vm,
        });
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemViewModel()
        {
            Priority = -10,
            Header = "File"
        });
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu/File", 
            new MenuItemViewModel()
            {
                Header = "New File",
                Command = new RelayCommand(() => _ = vm.ImportFileDialogAsync()),
                Icon = Application.Current?.FindResource("VsImageLib.NewFileCollection16X") as IImage
            },
            new MenuItemViewModel()
            {
                Header = "Open File",
                Command = new RelayCommand(() => _ = vm.OpenFileDialogAsync()),
                Icon = Application.Current?.FindResource("VsImageLib.NewFileCollection16X") as IImage
            });
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel()
        {
            Header = "Project Explorer",
            Command = new RelayCommand(() => _dockService.Show(containerProvider.Resolve<IProjectService>())),
            Icon = Application.Current?.FindResource("BoxIcons.RegularCode") as IImage,
        });
    }
}