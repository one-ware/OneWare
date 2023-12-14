using OneWare.SDK.Helpers;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Vhdl.Parsing;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Vhdl;

public class VhdlModule : IModule
{
    public const string LspName = "RustHDL";
    public const string LspPathSetting = "VhdlModule_RustHdlPath";
    
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var nativeToolService = containerProvider.Resolve<INativeToolService>();
        
        nativeToolService.Register(LspName, "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.77.0/vhdl_ls-x86_64-pc-windows-msvc.zip", PlatformId.WinX64)
            .WithShortcut("LSP", Path.Combine("vhdl_ls-x86_64-pc-windows-msvc", "bin" , "vhdl_ls.exe"), LspPathSetting);
        
        containerProvider.Resolve<ISettingsService>().RegisterTitledPath("Languages", "VHDL", LspPathSetting, "RustHDL Path", "Path for RustHDL executable", 
            nativeToolService.Get(LspName)!.GetShorcutPath("LSP")!,
            null, containerProvider.Resolve<IPaths>().PackagesDirectory, File.Exists);
        
        containerProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);
        containerProvider.Resolve<ILanguageManager>().RegisterTextMateLanguage("vhdl", "avares://OneWare.Vhdl/Assets/vhdl.tmLanguage.json", ".vhd", ".vhdl");
        containerProvider.Resolve<ILanguageManager>().RegisterService(typeof(LanguageServiceVhdl),true, ".vhd", ".vhdl");
        
        containerProvider.Resolve<FpgaService>().RegisterNodeProvider<VhdlNodeProvider>(".vhd", ".vhdl");
    }
}