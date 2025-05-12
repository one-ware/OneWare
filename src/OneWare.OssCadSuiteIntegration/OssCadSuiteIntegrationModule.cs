using Autofac;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.OssCadSuiteIntegration.Loaders;
using OneWare.OssCadSuiteIntegration.Simulators;
using OneWare.OssCadSuiteIntegration.Tools;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using Orientation = Avalonia.Layout.Orientation;
using OneWare.OssCadSuiteIntegration.Yosys;

namespace OneWare.OssCadSuiteIntegration;

public class OssCadSuiteIntegrationModule : Module
{
    public const string OssPathSetting = "OssCadSuite_Path";

    public static readonly Package OssCadPackage = new()
    {
        Category = "Binaries",
        Id = "osscadsuite",
        Type = "NativeTool",
        Name = "OSS CAD Suite",
        Description = "Open Source FPGA Tools",
        License = "ISC",
        IconUrl =
            "https://avatars.githubusercontent.com/u/35169771?s=48&v=4",
        Links =
        [
            new PackageLink
            {
                Name = "GitHub",
                Url = "https://github.com/YosysHQ/oss-cad-suite-build"
            }
        ],
        Tabs =
        [
            new PackageTab()
            {
                Title = "Readme",
                ContentUrl = "https://raw.githubusercontent.com/HendrikMennen/oss-cad-suite-build/main/README.md"
            },
            new PackageTab
            {
                Title = "License",
                ContentUrl = "https://raw.githubusercontent.com/YosysHQ/oss-cad-suite-build/main/COPYING"
            }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = "2024.07.27",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url =
                            "https://github.com/HendrikMennen/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-windows-x64-20240727.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url =
                            "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-linux-x64-20240727.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-x64",
                        Url =
                            "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-darwin-x64-20240727.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url =
                            "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-darwin-arm64-20240727.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    }
                ]
            },
            new PackageVersion
            {
                Version = "2025.01.22",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url =
                            "https://github.com/HendrikMennen/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-windows-x64-20250122.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url =
                            "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-linux-x64-20250122.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-x64",
                        Url =
                            "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-darwin-x64-20250122.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url =
                            "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-darwin-arm64-20250122.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    }
                ]
            },
        ]
    };

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<YosysService>().SingleInstance();
        builder.RegisterType<GtkWaveService>().SingleInstance();

        // You can register any additional services here as needed
    }

    public static async Task InitializeAsync(IContainer container)
    {
        var settingsService = container.Resolve<ISettingsService>();
        var yosysService = container.Resolve<YosysService>();
        var environmentService = container.Resolve<IEnvironmentService>();
        var windowService = container.Resolve<IWindowService>();
        var projectExplorerService = container.Resolve<IProjectExplorerService>();
        var fpgaService = container.Resolve<FpgaService>();
        var logger = container.Resolve<ILogger>();
        var packageService = container.Resolve<IPackageService>();
        var dockService = container.Resolve<IDockService>();
        var childProcessService = container.Resolve<IChildProcessService>();

        // Register the OSS CAD package
        packageService.RegisterPackage(OssCadPackage);

        // All the extension and UI registration logic remains the same here
        // You can move it from your current `OnInitialized()` method into here

        // Example (shortened):
        windowService.RegisterUiExtension("CompileWindow_TopRightExtension", new UiExtension(x =>
        {
            if (x is not UniversalFpgaProjectPinPlannerViewModel cm) return null;
            return new YosysCompileWindowExtensionView
            {
                DataContext = container.Resolve<YosysCompileWindowExtensionViewModel>(
                  new TypedParameter(typeof(UniversalFpgaProjectPinPlannerViewModel), cm))
            };
        }));

        // Register toolchains and simulators
        fpgaService.RegisterToolchain<YosysToolchain>();
        fpgaService.RegisterLoader<OpenFpgaLoader>();
        fpgaService.RegisterSimulator<IcarusVerilogSimulator>();

        settingsService.RegisterTitledFolderPath("Tools", "OSS Cad Suite", OssPathSetting, "OSS CAD Suite Path",
            "Sets the path for the Yosys OSS CAD Suite", "", null, null, IsOssPathValid);

        settingsService.GetSettingObservable<string>(OssPathSetting).Subscribe(x =>
        {
            if (string.IsNullOrEmpty(x)) return;

            if (!IsOssPathValid(x))
            {
                logger.Warning("OSS CAD Suite path invalid", null, false);
                return;
            }

            environmentService.SetPath("oss_bin", Path.Combine(x, "bin"));
            environmentService.SetPath("oss_pythonBin", Path.Combine(x, "py3bin"));
            environmentService.SetPath("oss_lib", Path.Combine(x, "lib"));
            environmentService.SetEnvironmentVariable("OPENFPGALOADER_SOJ_DIR", Path.Combine(x, "share", "openFPGALoader"));
            environmentService.SetEnvironmentVariable("PYTHON_EXECUTABLE", Path.Combine(x, "py3bin", $"python3{PlatformHelper.ExecutableExtension}"));
            environmentService.SetEnvironmentVariable("GHDL_PREFIX", Path.Combine(x, "lib", "ghdl"));
            environmentService.SetEnvironmentVariable("GTK_EXE_PREFIX", x);
            environmentService.SetEnvironmentVariable("GTK_DATA_PREFIX", x);
            environmentService.SetEnvironmentVariable("GDK_PIXBUF_MODULEDIR", Path.Combine(x, "lib", "gdk-pixbuf-2.0", "2.10.0", "loaders"));
            environmentService.SetEnvironmentVariable("GDK_PIXBUF_MODULE_FILE", Path.Combine(x, "lib", "gdk-pixbuf-2.0", "2.10.0", "loaders.cache"));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _ = childProcessService.ExecuteShellAsync(
                    $"gdk-pixbuf-query-loaders{PlatformHelper.ExecutableExtension}",
                    ["--update-cache"], x, "Updating gdk-pixbuf cache");
            }
        });

        projectExplorerService.RegisterConstructContextMenu((x, l) =>
        {
            if (x is [IProjectFile { Extension: ".v" } verilog])
                l.Add(new MenuItemViewModel("YosysNetList")
                {
                    Header = "Generate Json Netlist",
                    Command = new AsyncRelayCommand(() => yosysService.CreateNetListJsonAsync(verilog))
                });

            if (x is [IProjectFile { Extension: ".vcd" or ".ghw" or "fst" } wave] &&
                IsOssPathValid(settingsService.GetSettingValue<string>(OssPathSetting)))
                l.Add(new MenuItemViewModel("GtkWaveOpen")
                {
                    Header = "Open with GTKWave",
                    Command = new RelayCommand(() =>
                        container.Resolve<GtkWaveService>().OpenInGtkWave(wave.FullPath))
                });
        });

        dockService.RegisterFileOpenOverwrite(x =>
        {
            container.Resolve<GtkWaveService>().OpenInGtkWave(x.FullPath);
            return true;
        }, ".ghw", ".fst");
    }

    private static bool IsOssPathValid(string path)
    {
        if (!Directory.Exists(path)) return false;
        if (!File.Exists(Path.Combine(path, "bin", $"yosys{PlatformHelper.ExecutableExtension}"))) return false;
        if (!File.Exists(Path.Combine(path, "bin", $"openFPGALoader{PlatformHelper.ExecutableExtension}")))
            return false;
        return true;
    }
}
