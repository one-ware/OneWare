using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;
using OneWare.Essentials.ToolEngine.Strategies;
using OneWare.Essentials.ViewModels;
using OneWare.OssCadSuiteIntegration.Loaders;
using OneWare.OssCadSuiteIntegration.Simulators;
using OneWare.OssCadSuiteIntegration.Tools;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.OssCadSuiteIntegration.Yosys;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using Orientation = Avalonia.Layout.Orientation;

// ReSharper disable StringLiteralTypo

namespace OneWare.OssCadSuiteIntegration;

public class OssCadSuiteIntegrationModule : OneWareModuleBase
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
            new PackageTab
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
            new PackageVersion
            {
                Version = "2025.08.27",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url =
                            "https://github.com/hendrikmennen/oss-cad-suite-build/releases/download/2025-08-27/oss-cad-suite-windows-x64-20250827.tgz",
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
                            "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-08-27/oss-cad-suite-linux-x64-20250827.tgz",
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
                        Target = "linux-arm64",
                        Url =
                            "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-08-27/oss-cad-suite-linux-arm64-20250827.tgz",
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
                            "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-08-27/oss-cad-suite-darwin-x64-20250827.tgz",
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
                            "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-08-27/oss-cad-suite-darwin-arm64-20250827.tgz",
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
            }
        ]
    };

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<YosysService>();
        services.AddSingleton<GtkWaveService>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var yosysService = serviceProvider.Resolve<YosysService>();
        var environmentService = serviceProvider.Resolve<IEnvironmentService>();
        var windowService = serviceProvider.Resolve<IWindowService>();
        var projectExplorerService = serviceProvider.Resolve<IProjectExplorerService>();
        var fpgaService = serviceProvider.Resolve<FpgaService>();

        fpgaService.RegisterNodeProvider<YosysNodeProvider>();

        projectExplorerService.Projects.CollectionChanged += (sender, e) =>
        {
            if (sender is ObservableCollection<IProjectRoot> collection)
                if (e.Action == NotifyCollectionChangedAction.Add)
                    foreach (var project in collection)
                        YosysSettingHelper.SetConstraintOverlay(project);
        };

        var toolService = serviceProvider.Resolve<IToolService>();
        toolService.Register(new ToolContext("yosys", "Synth Tool", "yosys"), new NativeStrategy());

        toolService.Register(new ToolContext("nextpnr-ecp5", "Synth Tool", "nextpnr-ecp5"), new NativeStrategy());
        toolService.Register(new ToolContext("nextpnr-generic", "Synth Tool", "nextpnr-generic"), new NativeStrategy());
        toolService.Register(new ToolContext("nextpnr-himbaechel", "Synth Tool", " nextpnr-himbaechel"),
            new NativeStrategy());
        toolService.Register(new ToolContext("nextpnr-ice40", "Synth Tool", "nextpnr-ice40"), new NativeStrategy());
        toolService.Register(new ToolContext("nextpnr-machxo2", "Synth Tool", "nextpnr-machxo2"), new NativeStrategy());
        toolService.Register(new ToolContext("nextpnr-nexus", "Synth Tool", "nextpnr-nexus"), new NativeStrategy());

        toolService.Register(new ToolContext("openFPGALoader", "Synth Tool", "openFPGALoader"), new NativeStrategy());
        toolService.Register(new ToolContext("icepack", "Synth Tool", "icepack"), new NativeStrategy());
        toolService.Register(new ToolContext("iceprog", "Synth Tool", "iceprog"), new NativeStrategy());


        serviceProvider.Resolve<IPackageService>().RegisterPackage(OssCadPackage);
        serviceProvider.Resolve<IFileIconService>().RegisterFileIcon("VsImageLib2019.SettingsFile16X",
            ".pcf");

        serviceProvider.Resolve<IWindowService>().RegisterUiExtension("CompileWindow_TopRightExtension",
            new OneWareUiExtension(x =>
            {
                if (x is not UniversalFpgaProjectPinPlannerViewModel cm) return null;
                return new YosysCompileWindowExtensionView
                {
                    DataContext =
                        serviceProvider.Resolve<YosysCompileWindowExtensionViewModel>((
                            typeof(UniversalFpgaProjectPinPlannerViewModel), cm))
                };
            }));

        serviceProvider.Resolve<IWindowService>().RegisterUiExtension("UniversalFpgaToolBar_CompileMenuExtension",
            new OneWareUiExtension(x =>
            {
                if (x is not UniversalFpgaProjectRoot { Toolchain: YosysToolchain } root) return null;

                var name = root.Properties["Fpga"]?.ToString();
                var fpgaPackage = fpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == name);
                var fpga = fpgaPackage?.LoadFpga();

                return new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Children =
                    {
                        new MenuItem
                        {
                            Header = "Run Synthesis",
                            Command = new AsyncRelayCommand(async () =>
                            {
                                await projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                await yosysService.SynthAsync(root, new FpgaModel(fpga!));
                            }, () => fpga != null)
                        },
                        new MenuItem
                        {
                            Header = "Run Fit",
                            Command = new AsyncRelayCommand(async () =>
                            {
                                await projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                await yosysService.FitAsync(root, new FpgaModel(fpga!));
                            }, () => fpga != null)
                        },
                        new MenuItem
                        {
                            Header = "Run Assemble",
                            Command = new AsyncRelayCommand(async () =>
                            {
                                await projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                await yosysService.AssembleAsync(root, new FpgaModel(fpga!));
                            }, () => fpga != null)
                        },
                        new Separator { Width = double.NaN, Height = 1, Margin = new Thickness(0, 2, 0, 2) },
                        new MenuItem
                        {
                            Header = "Yosys Settings",
                            Icon = new Image
                            {
                                Source = Application.Current!.FindResource(
                                    Application.Current!.RequestedThemeVariant,
                                    "Material.SettingsOutline") as IImage
                            },
                            Command = new AsyncRelayCommand(async () =>
                            {
                                if (projectExplorerService
                                        .ActiveProject is UniversalFpgaProjectRoot fpgaProjectRoot)
                                {
                                    var selectedFpga = root.Properties["Fpga"]?.ToString();
                                    var selectedFpgaPackage =
                                        fpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == selectedFpga);

                                    if (selectedFpgaPackage == null)
                                    {
                                        serviceProvider.Resolve<ILogger>()
                                            .Warning("No FPGA Selected. Open Pin Planner first!");
                                        return;
                                    }

                                    await windowService.ShowDialogAsync(
                                        new YosysCompileSettingsView
                                        {
                                            DataContext = new YosysCompileSettingsViewModel(fpgaProjectRoot,
                                                selectedFpgaPackage.LoadFpga())
                                        });
                                }
                            })
                        },
                        new MenuItem
                        {
                            Header = "Open nextpnr GUI",
                            Command = new AsyncRelayCommand(async () =>
                            {
                                await projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                await yosysService.OpenNextpnrGuiAsync(root, new FpgaModel(fpga!));
                            }, () => fpga != null),
                            Icon = new Image
                            {
                                Source = Application.Current!.FindResource(
                                    Application.Current!.RequestedThemeVariant,
                                    "BoxIcons.RegularOpenGui") as IImage
                            }
                        }
                    }
                };
            }));

        serviceProvider.Resolve<IWindowService>().RegisterUiExtension(
            "UniversalFpgaToolBar_DownloaderConfigurationExtension", new OneWareUiExtension(x =>
            {
                if (x is not UniversalFpgaProjectRoot cm) return null;
                return new OpenFpgaLoaderWindowExtensionView
                {
                    DataContext =
                        serviceProvider.Resolve<OpenFpgaLoaderWindowExtensionViewModel>((
                            typeof(UniversalFpgaProjectRoot), cm))
                };
            }));

        serviceProvider.Resolve<FpgaService>().RegisterToolchain<YosysToolchain>();
        serviceProvider.Resolve<FpgaService>().RegisterLoader<OpenFpgaLoader>();
        serviceProvider.Resolve<FpgaService>().RegisterSimulator<IcarusVerilogSimulator>();

        settingsService.RegisterSetting("Tools", "OSS Cad Suite", OssPathSetting,
            new FolderPathSetting("OSS CAD Suite Path", "", null, null, IsOssPathValid)
            {
                HoverDescription = "Sets the path for the Yosys OSS CAD Suite"
            });

        settingsService.GetSettingObservable<string>(OssPathSetting).Subscribe(x =>
        {
            if (string.IsNullOrEmpty(x)) return;

            if (!IsOssPathValid(x))
            {
                serviceProvider.Resolve<ILogger>().Warning("OSS CAD Suite path invalid", null, false);
                return;
            }

            environmentService.SetPath("oss_bin", Path.Combine(x, "bin"));
            //environmentService.SetPath("oss_pythonBin", Path.Combine(x, "py3bin"));
            environmentService.SetPath("oss_lib", Path.Combine(x, "lib"));
            environmentService.SetEnvironmentVariable("QT_PLUGIN_PATH",
                Path.Combine(x, "lib", "qt5", "plugins"));
            environmentService.SetEnvironmentVariable("OPENFPGALOADER_SOJ_DIR",
                Path.Combine(x, "share", "openFPGALoader"));
            environmentService.SetEnvironmentVariable("PYTHON_EXECUTABLE",
                Path.Combine(x, "lib", $"python3{PlatformHelper.ExecutableExtension}"));
            //environmentService.SetEnvironmentVariable("VERILATOR_ROOT",
            //    Path.Combine(x, "share", $"verilator"));
            environmentService.SetEnvironmentVariable("GHDL_PREFIX",
                Path.Combine(x, "lib", "ghdl"));
            environmentService.SetEnvironmentVariable("GTK_EXE_PREFIX", x);
            environmentService.SetEnvironmentVariable("GTK_DATA_PREFIX", x);
            environmentService.SetEnvironmentVariable("GDK_PIXBUF_MODULEDIR",
                Path.Combine(x, "lib", "gdk-pixbuf-2.0", "2.10.0", "loaders"));
            environmentService.SetEnvironmentVariable("GDK_PIXBUF_MODULE_FILE",
                Path.Combine(x, "lib", "gdk-pixbuf-2.0", "2.10.0", "loaders.cache"));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                _ = serviceProvider.Resolve<IChildProcessService>().ExecuteShellAsync(
                    $"gdk-pixbuf-query-loaders{PlatformHelper.ExecutableExtension}",
                    ["--update-cache"], x, "Updating gdk-pixbuf cache");
        });

        serviceProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu((x, l) =>
        {
            if (x is [IProjectFile { Extension: ".v" } verilog])
                l.Add(new MenuItemViewModel("YosysNetList")
                {
                    Header = "Generate Json Netlist",
                    Command = new AsyncRelayCommand(() => yosysService.CreateJsonNetListAsync(verilog))
                });
            if (x is [IProjectFile { Extension: ".vcd" or ".ghw" or "fst" } wave] &&
                IsOssPathValid(settingsService.GetSettingValue<string>(OssPathSetting)))
                l.Add(new MenuItemViewModel("GtkWaveOpen")
                {
                    Header = "Open with GTKWave",
                    Command = new RelayCommand(() =>
                        serviceProvider.Resolve<GtkWaveService>().OpenInGtkWave(wave.FullPath))
                });
            if (x is [IProjectFile { Extension: ".pcf" } pcf])
                if (pcf.Root is UniversalFpgaProjectRoot universalFpgaProjectRoot)
                {
                    if (YosysSettingHelper.GetConstraintFile(universalFpgaProjectRoot) == pcf.RelativePath)
                        l.Add(new MenuItemViewModel("pcf")
                        {
                            Header = "Unset as Projects Constraint File",
                            Command = new AsyncRelayCommand(() => YosysSettingHelper.UpdateProjectPcFileAsync(pcf))
                        });
                    else
                        l.Add(new MenuItemViewModel("pcf")
                        {
                            Header = "Set as Projects Constraint File",
                            Command = new AsyncRelayCommand(() => YosysSettingHelper.UpdateProjectPcFileAsync(pcf))
                        });
                }
        });

        serviceProvider.Resolve<IMainDockService>().RegisterFileOpenOverwrite(x =>
        {
            serviceProvider.Resolve<GtkWaveService>().OpenInGtkWave(x.FullPath);
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