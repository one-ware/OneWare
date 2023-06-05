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

        _windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension", new ProjectExplorerMainWindowToolBarExtension()
        {
            DataContext = vm,
        });
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemModel("File")
        {
            Priority = -10,
            Header = "File"
        });
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu/File", 
            new MenuItemModel("New File")
            {
                Header = "New File",
                Command = new RelayCommand(() => _ = vm.ImportFileDialogAsync()),
                ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.NewFileCollection16X")
            },
            new MenuItemModel("Open File")
            {
                Header = "Open File",
                Command = new RelayCommand(() => _ = vm.OpenFileDialogAsync()),
                ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.NewFileCollection16X") 
            });
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemModel("Project Explorer")
        {
            Header = "Project Explorer",
            Command = new RelayCommand(() => _dockService.Show(containerProvider.Resolve<IProjectService>())),
            ImageIconObservable = Application.Current?.GetResourceObservable("BoxIcons.RegularCode") ,
        });
    }
}