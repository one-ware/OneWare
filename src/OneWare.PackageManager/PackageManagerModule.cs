using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.PackageManager.Installers;
using OneWare.PackageManager.Services;
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views;

namespace OneWare.PackageManager;

public class PackageManagerModule : OneWareModuleBase
{
    public static readonly Package OnnxRuntimeGpuLinuxPackage = new()
    {
        Category = "Runtimes",
        Id = "onnxruntime-gpu",
        Type = "OnnxRuntime",
        Name = "ONNX Runtime NVIDIA",
        Description = "Optional GPU runtime for ONNX Runtime. Available for Windows and Linux",
        License = "MIT",
        IconUrl = "https://raw.githubusercontent.com/microsoft/onnxruntime/refs/heads/main/ORT_icon_for_light_bg.png",
        Links =
        [
            new PackageLink
            {
                Name = "GitHub",
                Url = "https://github.com/microsoft/onnxruntime"
            }
        ],
        Tabs =
        [
            new PackageTab
            {
                Title = "License",
                ContentUrl = "https://raw.githubusercontent.com/microsoft/onnxruntime/main/LICENSE"
            }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = "1.23.2",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url = "https://www.nuget.org/api/v2/package/Microsoft.ML.OnnxRuntime.Gpu.Linux/1.23.2"
                    },
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url = "https://www.nuget.org/api/v2/package/Microsoft.ML.OnnxRuntime.Gpu.Windows/1.23.2"
                    },
                ]
            }
        ]
    };

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IPackageRepositoryClient, PackageRepositoryClient>();
        services.AddSingleton<IPackageCatalog, PackageCatalog>();
        services.AddSingleton<IPackageStateStore, PackageStateStore>();
        services.AddSingleton<IPackageDownloader, PackageDownloader>();
        services.AddSingleton<IPackageInstaller, PluginPackageInstaller>();
        services.AddSingleton<IPackageInstaller, NativeToolPackageInstaller>();
        services.AddSingleton<IPackageInstaller, OnnxRuntimePackageInstaller>();
        services.AddSingleton<IPackageInstaller, HardwarePackageInstaller>();
        services.AddSingleton<IPackageInstaller, LibraryPackageInstaller>();
        services.AddSingleton<IPackageService, PackageService>();
        services.AddSingleton<PackageManagerViewModel>();
        services.AddSingleton<IPackageWindowService>(provider => provider.Resolve<PackageManagerViewModel>());
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<IPackageService>().RegisterPackage(OnnxRuntimeGpuLinuxPackage);

        var windowService = serviceProvider.Resolve<IWindowService>();

        windowService.RegisterMenuItem("MainWindow_MainMenu/Extras", new MenuItemModel("Extensions")
        {
            Header = "Extensions",
            Command = new RelayCommand(() => windowService.Show(new PackageManagerView
            {
                DataContext = serviceProvider.Resolve<PackageManagerViewModel>()
            })),
            Icon = new IconModel("PackageManager")
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
