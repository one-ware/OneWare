using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.ProjectExplorer.Services;
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
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IFileWatchService, FileWatchService>();
        containerRegistry.RegisterManySingleton<ProjectExplorerViewModel>(typeof(IProjectExplorerService),
            typeof(ProjectExplorerViewModel));
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        if (containerProvider.Resolve<IProjectExplorerService>() is not ProjectExplorerViewModel vm) return;

        var dockService = containerProvider.Resolve<IDockService>();
        var windowService = containerProvider.Resolve<IWindowService>();
        
        dockService.RegisterLayoutExtension<IProjectExplorerService>(DockShowLocation.Left);

        windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension", new ProjectExplorerMainWindowToolBarExtension()
        {
            DataContext = vm,
        });
        
        windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemModel("File")
        {
            Priority = -10,
            Header = "File"
        });
        
        windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemModel("File")
            {
                Header = "File",
                Command = new RelayCommand(() => _ = vm.OpenFileDialogAsync()),
                ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.NewFileCollection16X") 
            });
        
        windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemModel("File")
            {
                Header = "File",
                Command = new RelayCommand(() => _ = vm.ImportFileDialogAsync()),
                ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.NewFileCollection16X")
            });
        
        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemModel("Project Explorer")
        {
            Header = "Project Explorer",
            Command = new RelayCommand(() => dockService.Show(containerProvider.Resolve<IProjectExplorerService>())),
            ImageIconObservable = Application.Current?.GetResourceObservable(ProjectExplorerViewModel.IconKey),
        });
    }
}