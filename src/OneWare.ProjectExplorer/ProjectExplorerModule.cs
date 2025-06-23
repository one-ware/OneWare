// OneWare.ProjectExplorer/ProjectExplorerModule.cs
using Autofac; // Essential for Autofac.Module
using OneWare.Essentials.Services;
using OneWare.ProjectExplorer.Services;
using OneWare.ProjectExplorer.ViewModels;

namespace OneWare.ProjectExplorer;

public class ProjectExplorerModule : Module // Inherit from Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Register types with Autofac
        builder.RegisterType<FileWatchService>().As<IFileWatchService>().SingleInstance();

        // Register ProjectExplorerViewModel as a singleton, implementing IProjectExplorerService
        builder.RegisterType<ProjectExplorerViewModel>()
               .AsSelf() // Register as ProjectExplorerViewModel
               .As<IProjectExplorerService>() // Also register as IProjectExplorerService
               .SingleInstance();

        // Register the initializer for this module as a singleton
        builder.RegisterType<ProjectExplorerModuleInitializer>().AsSelf().SingleInstance();

        base.Load(builder);
    }

    // The OnInitialized method will be removed from here.
    // Its logic will be moved to ProjectExplorerModuleInitializer.
}