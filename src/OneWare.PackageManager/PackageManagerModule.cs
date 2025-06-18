// OneWare.PackageManager/PackageManagerModule.cs
using Autofac; // Essential for Autofac.Module
using OneWare.PackageManager.Services;
using OneWare.PackageManager.ViewModels;

namespace OneWare.PackageManager;

public class PackageManagerModule : Module // Already inherits from Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Register types with Autofac
        builder.RegisterType<PackageService>().As<IPackageService>().SingleInstance();
        builder.RegisterType<PackageManagerViewModel>().AsSelf().SingleInstance(); // Register as self for direct injection

        // Register the initializer for this module as a singleton
        builder.RegisterType<PackageManagerModuleInitializer>().AsSelf().SingleInstance();

        base.Load(builder);
    }

    // The OnInitialized method will be removed from here.
    // Its logic will be moved to PackageManagerModuleInitializer.
}