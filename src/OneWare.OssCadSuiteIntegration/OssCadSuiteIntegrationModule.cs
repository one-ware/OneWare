using System.Runtime.InteropServices;
using OneWare.Shared.Helpers;
using OneWare.Shared.Services;
using Prism.Ioc;
using Prism.Modularity;
// ReSharper disable StringLiteralTypo

namespace OneWare.OssCadSuiteIntegration;

public class OssCadSuiteIntegrationModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var settingsService = containerProvider.Resolve<ISettingsService>();
        
        settingsService.RegisterTitledPath("Tools", "OSS Cad Suite", "OssCadSuite_Path", "OSS CAD Suite Path", 
            "Sets the path for the Yosys OSS CAD Suite", "", null, null, IsOssPathValid);

        string? environmentPathSetting;
        
        settingsService.GetSettingObservable<string>("OssCadSuite_Path").Subscribe(x =>
        {
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
    }

    private static bool IsOssPathValid(string path)
    {
        if (!Directory.Exists(path)) return false;
        if (!File.Exists(Path.Combine(path, "bin", $"yosys{PlatformHelper.ExecutableExtension}"))) return false;
        if (!File.Exists(Path.Combine(path, "bin", $"openFPGALoader{PlatformHelper.ExecutableExtension}"))) return false;
        return true;
    }
}