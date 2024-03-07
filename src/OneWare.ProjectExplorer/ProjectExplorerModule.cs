using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.ProjectExplorer.Services;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.ProjectExplorer.Views;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

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

        windowService.RegisterUiExtension<ProjectExplorerMainWindowToolBarExtension>("MainWindow_RoundToolBarExtension", vm);
        
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
                IconObservable = Application.Current?.GetResourceObservable("VsImageLib.NewFileCollection16X") 
            });
        
        windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemViewModel("File")
            {
                Header = "File",
                Command = new RelayCommand(() => _ = vm.ImportFileDialogAsync()),
                IconObservable = Application.Current?.GetResourceObservable("VsImageLib.NewFileCollection16X")
            });
        
        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Project Explorer")
        {
            Header = "Project Explorer",
            Command = new RelayCommand(() => dockService.Show(containerProvider.Resolve<IProjectExplorerService>())),
            IconObservable = Application.Current?.GetResourceObservable(ProjectExplorerViewModel.IconKey),
        });
    }
}