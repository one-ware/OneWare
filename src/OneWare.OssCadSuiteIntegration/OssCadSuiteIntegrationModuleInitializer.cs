// OneWare.OssCadSuiteIntegration/OssCadSuiteIntegrationModuleInitializer.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;
using OneWare.Essentials.Helpers; // For PlatformHelper
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.OssCadSuiteIntegration.Simulators;
using OneWare.OssCadSuiteIntegration.Tools;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.OssCadSuiteIntegration.Yosys;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using OneWare.OssCadSuiteIntegration.Loaders;
using OneWare.UniversalFpgaProjectSystem.Fpga; // Ensure 'Fpga' type is correctly referenced

namespace OneWare.OssCadSuiteIntegration
{
    public class OssCadSuiteIntegrationModuleInitializer
    {
        private readonly ISettingsService _settingsService;
        private readonly YosysService _yosysService;
        private readonly IEnvironmentService _environmentService;
        private readonly IWindowService _windowService;
        private readonly IProjectExplorerService _projectExplorerService;
        private readonly FpgaService _fpgaService;
        private readonly IPackageService _packageService;
        private readonly ILogger _logger;
        private readonly IChildProcessService _childProcessService;
        private readonly GtkWaveService _gtkWaveService;
        private readonly IDockService _dockService;
        private readonly PlatformHelper _platformHelper; // Injected PlatformHelper instance

        // Injected Func factories
        private readonly Func<UniversalFpgaProjectPinPlannerViewModel, YosysCompileWindowExtensionViewModel> _yosysCompileWindowExtensionVmFactory;
        private readonly Func<UniversalFpgaProjectRoot, OpenFpgaLoaderWindowExtensionViewModel> _openFpgaLoaderWindowExtensionVmFactory;
        private readonly Func<UniversalFpgaProjectRoot, IFpga, YosysCompileSettingsViewModel> _yosysCompileSettingsVmFactory;


        public OssCadSuiteIntegrationModuleInitializer(
            ISettingsService settingsService,
            YosysService yosysService,
            IEnvironmentService environmentService,
            IWindowService windowService,
            IProjectExplorerService projectExplorerService,
            FpgaService fpgaService,
            IPackageService packageService,
            ILogger logger,
            IChildProcessService childProcessService,
            PlatformHelper platformHelper, // Receive PlatformHelper via constructor injection
            GtkWaveService gtkWaveService,
            IDockService dockService,
            Func<UniversalFpgaProjectPinPlannerViewModel, YosysCompileWindowExtensionViewModel> yosysCompileWindowExtensionVmFactory,
            Func<UniversalFpgaProjectRoot, OpenFpgaLoaderWindowExtensionViewModel> openFpgaLoaderWindowExtensionVmFactory,
            Func<UniversalFpgaProjectRoot, IFpga, YosysCompileSettingsViewModel> yosysCompileSettingsVmFactory)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _yosysService = yosysService ?? throw new ArgumentNullException(nameof(yosysService));
            _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _projectExplorerService = projectExplorerService ?? throw new ArgumentNullException(nameof(projectExplorerService));
            _fpgaService = fpgaService ?? throw new ArgumentNullException(nameof(fpgaService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _childProcessService = childProcessService ?? throw new ArgumentNullException(nameof(childProcessService));
            _gtkWaveService = gtkWaveService ?? throw new ArgumentNullException(nameof(gtkWaveService));
            _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
            _platformHelper = platformHelper ?? throw new ArgumentNullException(nameof(platformHelper)); // Assign injected PlatformHelper

            _yosysCompileWindowExtensionVmFactory = yosysCompileWindowExtensionVmFactory ?? throw new ArgumentNullException(nameof(yosysCompileWindowExtensionVmFactory));
            _openFpgaLoaderWindowExtensionVmFactory = openFpgaLoaderWindowExtensionVmFactory ?? throw new ArgumentNullException(nameof(openFpgaLoaderWindowExtensionVmFactory));
            _yosysCompileSettingsVmFactory = yosysCompileSettingsVmFactory ?? throw new ArgumentNullException(nameof(yosysCompileSettingsVmFactory));
        }

        public void Initialize()
        {
            _packageService.RegisterPackage(OssCadSuiteIntegrationModule.OssCadPackage);

            _windowService.RegisterUiExtension("CompileWindow_TopRightExtension",
                new UiExtension(x =>
                {
                    if (x is not UniversalFpgaProjectPinPlannerViewModel cm) return null;
                    return new YosysCompileWindowExtensionView
                    {
                        DataContext = _yosysCompileWindowExtensionVmFactory(cm)
                    };
                }));

            _windowService.RegisterUiExtension("UniversalFpgaToolBar_CompileMenuExtension",
                new UiExtension(
                    x =>
                    {
                        if (x is not UniversalFpgaProjectRoot { Toolchain: YosysToolchain } root) return null;

                        var name = root.Properties["Fpga"]?.ToString();
                        var fpgaPackage = _fpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == name);
                        var fpga = fpgaPackage?.LoadFpga();

                        return new StackPanel()
                        {
                            Orientation = Avalonia.Layout.Orientation.Vertical,
                            Children =
                            {
                                new MenuItem()
                                {
                                    Header = "Run Synthesis",
                                    Command = new AsyncRelayCommand(async () =>
                                    {
                                        await _projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                        await _yosysService.SynthAsync(root, new FpgaModel(fpga!,null));
                                    }, () => fpga != null)
                                },
                                new MenuItem()
                                {
                                    Header = "Run Fit",
                                    Command = new AsyncRelayCommand(async () =>
                                    {
                                        await _projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                        await _yosysService.FitAsync(root, new FpgaModel(fpga!,null));
                                    }, () => fpga != null)
                                },
                                new MenuItem()
                                {
                                    Header = "Run Assemble",
                                    Command = new AsyncRelayCommand(async () =>
                                    {
                                        await _projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                        await _yosysService.AssembleAsync(root, new FpgaModel(fpga!,null));
                                    }, () => fpga != null)
                                },
                                new Separator(),
                                new MenuItem()
                                {
                                    Header = "Yosys Settings",
                                    Icon = new Image()
                                    {
                                        Source = Application.Current!.FindResource(
                                            Application.Current!.RequestedThemeVariant,
                                            "Material.SettingsOutline") as IImage
                                    },
                                    Command = new AsyncRelayCommand(async () =>
                                    {
                                        if (_projectExplorerService
                                                .ActiveProject is UniversalFpgaProjectRoot fpgaProjectRoot)
                                        {
                                            var selectedFpga = root.Properties["Fpga"]?.ToString();
                                            var selectedFpgaPackage =
                                                _fpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == selectedFpga);
                                            var fpga = fpgaPackage?.LoadFpga();

                                            if (selectedFpgaPackage == null)
                                            {
                                                _logger.Warning("No FPGA Selected. Open Pin Planner first!");
                                                return;
                                            }

                                            await _windowService.ShowDialogAsync(
                                                new YosysCompileSettingsView
                                                {
                                                    DataContext = new YosysCompileSettingsViewModel(fpgaProjectRoot,
                                                    selectedFpgaPackage.LoadFpga())
                                                });
                                        }
                                    })
                                }
                            }
                        };
                    }));

            _windowService.RegisterUiExtension(
                "UniversalFpgaToolBar_DownloaderConfigurationExtension", new UiExtension(x =>
                {
                    if (x is not UniversalFpgaProjectRoot cm) return null;
                    return new OpenFpgaLoaderWindowExtensionView
                    {
                        DataContext = _openFpgaLoaderWindowExtensionVmFactory(cm)
                    };
                }));

            _fpgaService.RegisterToolchain<YosysToolchain>();
            _fpgaService.RegisterLoader<OpenFpgaLoader>();
            _fpgaService.RegisterSimulator<IcarusVerilogSimulator>();

            // Pass the instance method for validation
            _settingsService.RegisterTitledFolderPath("Tools", "OSS Cad Suite", OssCadSuiteIntegrationModule.OssPathSetting, "OSS CAD Suite Path",
                "Sets the path for the Yosys OSS CAD Suite", "", null, null, IsOssPathValid);

            _settingsService.GetSettingObservable<string>(OssCadSuiteIntegrationModule.OssPathSetting).Subscribe(x =>
            {
                if (string.IsNullOrEmpty(x)) return;

                if (!IsOssPathValid(x)) // Use the instance method
                {
                    _logger.Warning("OSS CAD Suite path invalid", null, false);
                    return;
                }

                _environmentService.SetPath("oss_bin", Path.Combine(x, "bin"));
                _environmentService.SetPath("oss_pythonBin", Path.Combine(x, "py3bin"));
                _environmentService.SetPath("oss_lib", Path.Combine(x, "lib"));
                _environmentService.SetEnvironmentVariable("OPENFPGALOADER_SOJ_DIR",
                    Path.Combine(x, "share", "openFPGALoader"));
                _environmentService.SetEnvironmentVariable("PYTHON_EXECUTABLE",
                    Path.Combine(x, "py3bin", $"python3{_platformHelper.ExecutableExtension}")); // Use injected _platformHelper
                _environmentService.SetEnvironmentVariable("GHDL_PREFIX",
                    Path.Combine(x, "lib", $"ghdl"));
                _environmentService.SetEnvironmentVariable("GTK_EXE_PREFIX", x);
                _environmentService.SetEnvironmentVariable("GTK_DATA_PREFIX", x);
                _environmentService.SetEnvironmentVariable("GDK_PIXBUF_MODULEDIR",
                    Path.Combine(x, "lib", "gdk-pixbuf-2.0", "2.10.0", "loaders"));
                _environmentService.SetEnvironmentVariable("GDK_PIXBUF_MODULE_FILE",
                    Path.Combine(x, "lib", "gdk-pixbuf-2.0", "2.10.0", "loaders.cache"));

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    _ = _childProcessService.ExecuteShellAsync(
                        $"gdk-pixbuf-query-loaders{_platformHelper.ExecutableExtension}", // Use injected _platformHelper
                        ["--update-cache"], x, "Updating gdk-pixbuf cache");
            });

            _projectExplorerService.RegisterConstructContextMenu((x, l) =>
            {
                if (x is [IProjectFile { Extension: ".v" } verilog])
                    l.Add(new MenuItemViewModel("YosysNetList")
                    {
                        Header = "Generate Json Netlist",
                        Command = new AsyncRelayCommand(() => _yosysService.CreateNetListJsonAsync(verilog))
                    });
                if (x is [IProjectFile { Extension: ".vcd" or ".ghw" or "fst" } wave] &&
                    IsOssPathValid(_settingsService.GetSettingValue<string>(OssCadSuiteIntegrationModule.OssPathSetting))) // Use the instance method
                    l.Add(new MenuItemViewModel("GtkWaveOpen")
                    {
                        Header = "Open with GTKWave",
                        Command = new RelayCommand(() =>
                            _gtkWaveService.OpenInGtkWave(wave.FullPath))
                    });
            });

            _dockService.RegisterFileOpenOverwrite(x =>
            {
                _gtkWaveService.OpenInGtkWave(x.FullPath);
                return true;
            }, ".ghw", ".fst");
        }

        // Instance method now, using the injected _platformHelper
        private bool IsOssPathValid(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!Directory.Exists(path)) return false;
            if (!File.Exists(Path.Combine(path, "bin", $"yosys{_platformHelper.ExecutableExtension}"))) return false;
            if (!File.Exists(Path.Combine(path, "bin", $"openFPGALoader{_platformHelper.ExecutableExtension}")))
                return false;
            return true;
        }
    }
}