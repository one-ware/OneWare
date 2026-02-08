using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.PackageManager.Installers;
using OneWare.PackageManager.Services;
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views;

namespace OneWare.PackageManager;

public class PackageManagerModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IPackageRepositoryClient, PackageRepositoryClient>();
        services.AddSingleton<IPackageCatalog, PackageCatalog>();
        services.AddSingleton<IPackageStateStore, PackageStateStore>();
        services.AddSingleton<IPackageDownloader, PackageDownloader>();
        services.AddSingleton<IPackageInstaller, PluginPackageInstaller>();
        services.AddSingleton<IPackageInstaller, NativeToolPackageInstaller>();
        services.AddSingleton<IPackageInstaller, HardwarePackageInstaller>();
        services.AddSingleton<IPackageInstaller, LibraryPackageInstaller>();
        services.AddSingleton<IPackageService, PackageService>();
        services.AddSingleton<PackageManagerViewModel>();
        services.AddSingleton<IPackageWindowService>(provider => provider.Resolve<PackageManagerViewModel>());
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var windowService = serviceProvider.Resolve<IWindowService>();

        windowService.RegisterMenuItem("MainWindow_MainMenu/Extras", new MenuItemViewModel("Extensions")
        {
            Header = "Extensions",
            Command = new RelayCommand(() => windowService.Show(new PackageManagerView
            {
                DataContext = serviceProvider.Resolve<PackageManagerViewModel>()
            })),
            IconModel = new IconModel("PackageManager")
        });

        serviceProvider.Resolve<ISettingsService>().RegisterSettingCategory("Package Manager", 0, "PackageManager");

        serviceProvider.Resolve<ISettingsService>()
            .RegisterSetting("Package Manager", "Sources", "PackageManager_Sources",
                new ListBoxSetting("Custom Package Sources")
                {
                    MarkdownDocumentation = """
                                            Add custom package sources to the package manager. These sources will be used to search for and install packages.
                                            You can add either:
                                            - A Package Repository
                                            - A Direct link to a package manifest
                                            """
                });
    }
}
