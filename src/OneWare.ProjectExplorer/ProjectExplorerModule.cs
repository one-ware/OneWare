using Avalonia;
using Avalonia.Controls;
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
        containerRegistry.RegisterManySingleton<ProjectExplorerViewModel>(typeof(IProjectExplorerService),
            typeof(ProjectExplorerViewModel));
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        if (containerProvider.Resolve<IProjectExplorerService>() is not ProjectExplorerViewModel vm) return;
        
        _dockService.RegisterLayoutExtension<IProjectExplorerService>(DockShowLocation.Left);

        _windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension", new ProjectExplorerMainWindowToolBarExtension()
        {
            DataContext = vm,
        });
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemModel("File")
        {
            Priority = -10,
            Header = "File"
        });
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemModel("File")
            {
                Header = "File",
                Command = new RelayCommand(() => _ = vm.OpenFileDialogAsync()),
                ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.NewFileCollection16X") 
            });
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemModel("File")
            {
                Header = "File",
                Command = new RelayCommand(() => _ = vm.ImportFileDialogAsync()),
                ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.NewFileCollection16X")
            });
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemModel("Project Explorer")
        {
            Header = "Project Explorer",
            Command = new RelayCommand(() => _dockService.Show(containerProvider.Resolve<IProjectExplorerService>())),
            ImageIconObservable = Application.Current?.GetResourceObservable(ProjectExplorerViewModel.IconKey),
        });
    }
}