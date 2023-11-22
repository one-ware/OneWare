using OneWare.SDK.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Verilog.Parsing;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Verilog;

public class VerilogModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IErrorService>().RegisterErrorSource("Verible");
        containerProvider.Resolve<ILanguageManager>().RegisterTextMateLanguage("verilog", "avares://OneWare.Verilog/Assets/verilog.tmLanguage.json", ".v", ".sv");
        containerProvider.Resolve<ILanguageManager>().RegisterService(typeof(LanguageServiceVerilog),true, ".v", ".sv");
        containerProvider.Resolve<FpgaService>().RegisterNodeProvider<VerilogNodeProvider>(".v", ".sv");
    }
}