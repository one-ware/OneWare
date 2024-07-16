using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
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
using Prism.Ioc;
using Prism.Modularity;

// ReSharper disable StringLiteralTypo

namespace OneWare.OssCadSuiteIntegration;

public class OssCadSuiteIntegrationModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<YosysService>();
        containerRegistry.RegisterSingleton<GtkWaveService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var settingsService = containerProvider.Resolve<ISettingsService>();
        var yosysService = containerProvider.Resolve<YosysService>();
        var environmentService = containerProvider.Resolve<IEnvironmentService>();

        containerProvider.Resolve<IWindowService>().RegisterUiExtension("CompileWindow_TopRightExtension",
            new UiExtension(x =>
            {
                if (x is not UniversalFpgaProjectCompileViewModel cm) return null;
                return new YosysCompileWindowExtensionView
                {
                    DataContext =
                        containerProvider.Resolve<YosysCompileWindowExtensionViewModel>((
                            typeof(UniversalFpgaProjectCompileViewModel), cm))
                };
            }));
        containerProvider.Resolve<IWindowService>().RegisterUiExtension(
            "UniversalFpgaToolBar_DownloaderConfigurationExtension", new UiExtension(x =>
            {
                if (x is not UniversalFpgaProjectRoot cm) return null;
                return new OpenFpgaLoaderWindowExtensionView
                {
                    DataContext =
                        containerProvider.Resolve<OpenFpgaLoaderWindowExtensionViewModel>((
                            typeof(UniversalFpgaProjectRoot), cm))
                };
            }));
        containerProvider.Resolve<FpgaService>().RegisterToolchain<YosysToolchain>();
        containerProvider.Resolve<FpgaService>().RegisterLoader<OpenFpgaLoader>();
        containerProvider.Resolve<FpgaService>().RegisterSimulator<IcarusVerilogSimulator>();

        settingsService.RegisterTitledFolderPath("Tools", "OSS Cad Suite", "OssCadSuite_Path", "OSS CAD Suite Path",
            "Sets the path for the Yosys OSS CAD Suite", "", null, null, IsOssPathValid);

        settingsService.GetSettingObservable<string>("OssCadSuite_Path").Subscribe(x =>
        {
            if (string.IsNullOrEmpty(x)) return;

            if (!IsOssPathValid(x))
            {
                containerProvider.Resolve<ILogger>().Warning("OSS CAD Suite path invalid", null, false);
                return;
            }

            environmentService.SetPath("oss_bin", Path.Combine(x, "bin"));
            environmentService.SetPath("oss_pythonBin", Path.Combine(x, "py3bin"));
            environmentService.SetPath("oss_lib", Path.Combine(x, "lib"));
            environmentService.SetEnvironmentVariable("OPENFPGALOADER_SOJ_DIR",
                Path.Combine(x, "share", "openFPGALoader"));
            environmentService.SetEnvironmentVariable("PYTHON_EXECUTABLE",
                Path.Combine(x, "py3bin", $"python3{PlatformHelper.ExecutableExtension}"));
        });

        containerProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu((x, l) =>
        {
            if (x is [IProjectFile { Extension: ".v" } verilog])
                l.Add(new MenuItemViewModel("YosysNetList")
                {
                    Header = "Generate Json Netlist",
                    Command = new AsyncRelayCommand(() => yosysService.CreateNetListJsonAsync(verilog))
                });
            if (x is [IProjectFile { Extension: ".vcd" or ".ghw" or "fst" } wave] &&
                IsOssPathValid(settingsService.GetSettingValue<string>("OssCadSuite_Path")))
                l.Add(new MenuItemViewModel("GtkWaveOpen")
                {
                    Header = "Open with GTKWave",
                    Command = new RelayCommand(() =>
                        containerProvider.Resolve<GtkWaveService>().OpenInGtkWave(wave.FullPath))
                });
        });
        
        containerProvider.Resolve<IDockService>().RegisterFileOpenOverwrite(x =>
        {
            containerProvider.Resolve<GtkWaveService>().OpenInGtkWave(x.FullPath);
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