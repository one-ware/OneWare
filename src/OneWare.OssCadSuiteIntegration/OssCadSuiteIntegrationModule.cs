using CommunityToolkit.Mvvm.Input;
using OneWare.OssCadSuiteIntegration.Loaders;
using OneWare.OssCadSuiteIntegration.Yosys;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using Prism.Modularity;
// ReSharper disable StringLiteralTypo

namespace OneWare.OssCadSuiteIntegration;

public class OssCadSuiteIntegrationModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<YosysService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var settingsService = containerProvider.Resolve<ISettingsService>();
        var yosysService = containerProvider.Resolve<YosysService>();

        containerProvider.Resolve<FpgaService>().RegisterToolchain<YosysToolchain>();
        containerProvider.Resolve<FpgaService>().RegisterLoader<OpenFpgaLoader>();
        
        settingsService.RegisterTitledPath("Tools", "OSS Cad Suite", "OssCadSuite_Path", "OSS CAD Suite Path", 
            "Sets the path for the Yosys OSS CAD Suite", "", null, null, IsOssPathValid);

        string? environmentPathSetting;
        
        settingsService.GetSettingObservable<string>("OssCadSuite_Path").Subscribe(x =>
        {
            if (string.IsNullOrEmpty(x)) return;

            if (!IsOssPathValid(x))
            {
                containerProvider.Resolve<ILogger>().Warning("OSS CAD Suite path invalid", null, false);
                return;
            }
            
            var binPath = Path.Combine(x, "bin");
            var pythonBin = Path.Combine(x, "py3bin");
            var lib = Path.Combine(x, "lib");
                
            environmentPathSetting = PlatformHelper.Platform switch
            {
                PlatformId.WinX64 or PlatformId.WinArm64 => $";{binPath};{pythonBin};{lib};",
                _ => $":{binPath}:{pythonBin}:{lib}:"
            };
            
            var currentPath = Environment.GetEnvironmentVariable("PATH");
            
            
            //TODO Add all
            Environment.SetEnvironmentVariable("PATH", $"{environmentPathSetting}{currentPath}");
            Environment.SetEnvironmentVariable("OPENFPGALOADER_SOJ_DIR", Path.Combine(x, "share", "openFPGALoader"));
            Environment.SetEnvironmentVariable("PYTHON_EXECUTABLE", Path.Combine(x, "py3bin", $"python3{PlatformHelper.ExecutableExtension}"));
        });
        
        containerProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu(x =>
        {
            if (x is [UniversalFpgaProjectRoot project])
            {
                return new[]
                {
                    new MenuItemViewModel("Yosys")
                    {
                        Header = "Compile with yosys",
                        Command = new AsyncRelayCommand(() => yosysService.SynthAsync(project))
                    }
                };
            }
            
            if (x is [IProjectFile {Extension: ".v"} verilog])
            {
                return new[]
                {
                    new MenuItemViewModel("YosysNetList")
                    {
                        Header = "Generate Json Netlist",
                        Command = new AsyncRelayCommand(() => yosysService.CreateNetListJsonAsync(verilog))
                    }
                };
            }
            return null;
        });
    }

    private static bool IsOssPathValid(string path)
    {
        if (!Directory.Exists(path)) return false;
        if (!File.Exists(Path.Combine(path, "bin", $"yosys{PlatformHelper.ExecutableExtension}"))) return false;
        if (!File.Exists(Path.Combine(path, "bin", $"openFPGALoader{PlatformHelper.ExecutableExtension}"))) return false;
        return true;
    }
}