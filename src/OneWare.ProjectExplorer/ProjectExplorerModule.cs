using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.ProjectExplorer.Services;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.ProjectExplorer.Views;

namespace OneWare.ProjectExplorer;

public class ProjectExplorerModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IFileWatchService, FileWatchService>();
        services.AddSingleton<ProjectExplorerViewModel>();
        services.AddSingleton<IProjectExplorerService>(provider => provider.Resolve<ProjectExplorerViewModel>());
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        if (serviceProvider.Resolve<IProjectExplorerService>() is not ProjectExplorerViewModel vm) return;

        var dockService = serviceProvider.Resolve<IMainDockService>();
        var windowService = serviceProvider.Resolve<IWindowService>();

        dockService.RegisterLayoutExtension<IProjectExplorerService>(DockShowLocation.Left);

        windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension", new OneWareUiExtension(_ =>
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
                Priority = 10,
                IconObservable = Application.Current!.GetResourceObservable("VsImageLib.NewFileCollection16X")
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemViewModel("Project Explorer")
            {
                Header = "Project Explorer",
                Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<IProjectExplorerService>())),
                IconObservable = Application.Current!.GetResourceObservable(ProjectExplorerViewModel.IconKey)
            });
    }
}

