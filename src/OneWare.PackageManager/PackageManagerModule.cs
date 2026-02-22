using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Helpers;
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
    public static readonly Package OnnxRuntimeNvidiaPackage = new()
    {
        Category = "ONNX Runtimes",
        Id = "onnxruntime-nvidia",
        Type = "OnnxRuntime",
        Name = "ONNX Runtime NVIDIA",
        Description = "ONNXRuntime Available for Windows and Linux",
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

    public static readonly Package OnnxRuntimeDirectMlPackage = new()
    {
        Category = "ONNX Runtimes",
        Id = "onnxruntime-directml",
        Type = "OnnxRuntime",
        Name = "ONNX Runtime DirectML",
        Description = "ONNX Runtime with DirectML for Windows",
        License = "MIT",
        IconUrl = "https://raw.githubusercontent.com/microsoft/onnxruntime/refs/heads/main/ORT_icon_for_light_bg.png",
        Links =
        [
            new PackageLink
            {
                Name = "NuGet",
                Url = "https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.DirectML/1.23.0"
            },
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
                Version = "1.23.0",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url = "https://www.nuget.org/api/v2/package/Microsoft.ML.OnnxRuntime.DirectML/1.23.0"
                    },
                    new PackageTarget
                    {
                        Target = "win-arm64",
                        Url = "https://www.nuget.org/api/v2/package/Microsoft.ML.OnnxRuntime.DirectML/1.23.0"
                    }
                ]
            }
        ]
    };

    public static readonly Package OnnxRuntimeOpenVinoPackage = new()
    {
        Category = "ONNX Runtimes",
        Id = "onnxruntime-openvino",
        Type = "OnnxRuntime",
        Name = "ONNX Runtime OpenVINO",
        Description = "ONNX Runtime with OpenVINO execution provider",
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
                Version = "1.23.0",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url = "https://www.nuget.org/api/v2/package/Intel.ML.OnnxRuntime.OpenVino/1.23.0"
                    }
                ]
            }
        ]
    };

    public static readonly Package OnnxRuntimeQnnPackage = new()
    {
        Category = "ONNX Runtimes",
        Id = "onnxruntime-qnn",
        Type = "OnnxRuntime",
        Name = "ONNX Runtime QNN",
        Description = "ONNX Runtime with Qualcomm QNN execution provider",
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
                        Target = "win-arm64",
                        Url = "https://www.nuget.org/api/v2/package/Microsoft.ML.OnnxRuntime.QNN/1.23.2"
                    }
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
        services.AddSingleton<PluginPackageInstaller>();
        services.AddSingleton<NativeToolPackageInstaller>();
        services.AddSingleton<OnnxRuntimePackageInstaller>();
        services.AddSingleton<HardwarePackageInstaller>();
        services.AddSingleton<LibraryPackageInstaller>();
        services.AddSingleton<GenericPackageInstaller>();
        services.AddSingleton<IPackageService, PackageService>();
        services.AddSingleton<PackageManagerViewModel>();
        services.AddSingleton<IPackageWindowService>(provider => provider.Resolve<PackageManagerViewModel>());
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var packageService = serviceProvider.Resolve<IPackageService>();
        packageService.RegisterInstaller<PluginPackageInstaller>("Plugin");
        packageService.RegisterInstaller<NativeToolPackageInstaller>("NativeTool");
        packageService.RegisterInstaller<OnnxRuntimePackageInstaller>("OnnxRuntime");
        packageService.RegisterInstaller<HardwarePackageInstaller>("Hardware");
        packageService.RegisterInstaller<LibraryPackageInstaller>("Library");

        if(PlatformHelper.Platform is PlatformId.LinuxX64 or PlatformId.WinX64)
            packageService.RegisterPackage(OnnxRuntimeNvidiaPackage);
        
        if(PlatformHelper.Platform is PlatformId.WinX64 or PlatformId.WinArm64)
            packageService.RegisterPackage(OnnxRuntimeDirectMlPackage);
        
        if(PlatformHelper.Platform is PlatformId.WinX64)
            packageService.RegisterPackage(OnnxRuntimeOpenVinoPackage);
        
        if(PlatformHelper.Platform is PlatformId.WinArm64)
            packageService.RegisterPackage(OnnxRuntimeQnnPackage);

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
