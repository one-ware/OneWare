using OneWare.SDK.Helpers;
using OneWare.SDK.NativeTools;
using OneWare.SDK.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Verilog.Parsing;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Verilog;

public class VerilogModule : IModule
{
    public const string LspName = "Verible";
    public const string LspPathSetting = "VerilogModule_VeriblePath";

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var nativeToolService = containerProvider.Resolve<INativeToolService>();
        
        var nativeTool = nativeToolService.Register(LspName);
            
        nativeTool.AddPlatform(PlatformId.WinX64, "https://github.com/chipsalliance/verible/releases/download/v0.0-3582-g25611a89/verible-v0.0-3582-g25611a89-win64.zip")
            .WithShortcut("LSP", Path.Combine("verible-v0.0-3582-g25611a89-win64", "verible-verilog-ls.exe"),
                LspPathSetting);
        
        nativeTool.AddPlatform(PlatformId.LinuxX64, "https://github.com/chipsalliance/verible/releases/download/v0.0-3582-g25611a89/verible-v0.0-3582-g25611a89-linux-static-x86_64.tar.gz")
            .WithShortcut("LSP", Path.Combine("verible-v0.0-3582-g25611a89", "bin", "verible-verilog-ls"),
                LspPathSetting);
        
        nativeTool.AddPlatform(PlatformId.OsxArm64, "https://github.com/chipsalliance/verible/releases/download/v0.0-3582-g25611a89/verible-v0.0-3582-g25611a89-macOS.tar.gz")
            .WithShortcut("LSP", Path.Combine("verible-v0.0-3582-g25611a89", "bin", "verible-verilog-ls"),
                LspPathSetting);

        containerProvider.Resolve<ISettingsService>().RegisterTitledPath("Languages", "Verilog", LspPathSetting,
            "Verible Path", "Path for Verible executable",
            nativeToolService.Get(LspName)!.GetShortcutPathOrEmpty("LSP"),
            null, containerProvider.Resolve<IPaths>().PackagesDirectory, File.Exists);

        containerProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);
        containerProvider.Resolve<ILanguageManager>().RegisterTextMateLanguage("verilog",
            "avares://OneWare.Verilog/Assets/verilog.tmLanguage.json", ".v", ".sv");
        containerProvider.Resolve<ILanguageManager>()
            .RegisterService(typeof(LanguageServiceVerilog), true, ".v", ".sv");
        containerProvider.Resolve<FpgaService>().RegisterNodeProvider<VerilogNodeProvider>(".v", ".sv");
    }
}