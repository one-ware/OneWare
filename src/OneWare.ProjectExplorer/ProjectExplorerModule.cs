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
                Icon = new IconModel("VsImageLib.NewFileCollection16X")
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemModel("File")
            {
                Header = "File",
                Command = new RelayCommand(() => _ = vm.ImportFileDialogAsync()),
                Priority = 10,
                Icon = new IconModel("VsImageLib.NewFileCollection16X")
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("Project Explorer")
            {
                Header = "Project Explorer",
                Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<IProjectExplorerService>(), DockShowLocation.Left)),
                Icon = new IconModel(ProjectExplorerViewModel.IconKey)
            });
    }
}