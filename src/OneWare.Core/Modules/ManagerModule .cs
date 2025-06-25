using Autofac;
using OneWare.Essentials.Services;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.Settings;

namespace OneWare.Core.Modules
{
    public class ManagerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Register services related to PackageManagerModule here
            // Example:
            // builder.RegisterType<PackageManagerService>().As<IPackageManagerService>().SingleInstance();
            // builder.RegisterType<SomePackageManagerViewModel>().AsSelf();

            // If PackageManagerModule itself contains an initialization method,
            // you might need to adapt it or move its logic to where it's consumed.

            // Register the PackageManager's main entry point or any services
            // that were registered in its original Prism module.
            // For example, if PackageManagerModule registered an IPackageService:
            builder.RegisterType<SettingsService>().As<ISettingsService>().SingleInstance();
            builder.RegisterType<ProjectExplorerViewModel>().As<IProjectExplorerService>().SingleInstance();

        }
    }
}
