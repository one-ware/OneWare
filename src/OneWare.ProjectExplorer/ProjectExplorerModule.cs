using System;
using Avalonia;
using Avalonia.Controls;
using Autofac;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.ProjectExplorer.Services;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.ProjectExplorer.Views;
using Autofac.Core.Registration;
using Autofac.Core;

namespace OneWare.ProjectExplorer;

public class ProjectExplorerModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Register services
        builder.RegisterType<FileWatchService>()
            .As<IFileWatchService>()
            .SingleInstance();

        // Register ProjectExplorerViewModel as both itself and IProjectExplorerService
        builder.RegisterType<ProjectExplorerViewModel>()
            .AsSelf()
            .As<IProjectExplorerService>()
            .SingleInstance();
    }

    protected override void AttachToComponentRegistration(IComponentRegistryBuilder componentRegistry, IComponentRegistration registration)
    {
        // No need to override this unless doing advanced things
    }

    // Optional OnInitialized Hook: Autofac doesn't have this directly like Prism.
    // You'll typically do this in your App startup logic after container is built.

    public static void OnInitialized(ILifetimeScope scope)
    {
        if (scope.Resolve<IProjectExplorerService>() is not ProjectExplorerViewModel vm)
            return;

        var dockService = scope.Resolve<IDockService>();
        var windowService = scope.Resolve<IWindowService>();

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
                Command = new RelayCommand(() =>
                    dockService.Show(scope.Resolve<IProjectExplorerService>())),
                IconObservable = Application.Current!.GetResourceObservable(ProjectExplorerViewModel.IconKey)
            });
    }
}
