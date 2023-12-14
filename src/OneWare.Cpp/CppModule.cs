using OneWare.SDK.Helpers;
using OneWare.SDK.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Cpp;

public class CppModule : IModule
{
    public const string LspName = "clangd";
    public const string LspPathSetting = "CppModule_ClangdPath";

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
         
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var nativeToolService = containerProvider.Resolve<INativeToolService>();

        nativeToolService.Register(LspName, "https://github.com/clangd/clangd/releases/download/17.0.3/clangd-windows-17.0.3.zip", PlatformId.WinX64)
            .WithShortcut("LSP", Path.Combine("clangd_17.0.3", "bin", "clangd.exe"), LspPathSetting);
        
        containerProvider.Resolve<ISettingsService>().RegisterTitledPath("Languages", "C++", LspPathSetting, "Clangd Path", "Path for clangd executable", 
            nativeToolService.Get(LspName)!.GetShorcutPath("LSP")!,
            null, containerProvider.Resolve<IPaths>().PackagesDirectory, File.Exists);
        
        containerProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);
        
        containerProvider.Resolve<ILanguageManager>().RegisterService(typeof(LanguageServiceCpp),false, ".cpp", ".h", ".c", ".hpp");
    }
}