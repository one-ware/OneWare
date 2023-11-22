using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Vhdl.Parsing;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Vhdl;

public class VhdlModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IErrorService>().RegisterErrorSource("RustHDL");
        containerProvider.Resolve<ILanguageManager>().RegisterTextMateLanguage("vhdl", "avares://OneWare.Vhdl/Assets/vhdl.tmLanguage.json", ".vhd", ".vhdl");
        containerProvider.Resolve<ILanguageManager>().RegisterService(typeof(LanguageServiceVhdl),true, ".vhd", ".vhdl");
        
        containerProvider.Resolve<FpgaService>().RegisterNodeProvider<VhdlNodeProvider>(".vhd", ".vhdl");
    }
}