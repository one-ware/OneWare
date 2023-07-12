using OneWare.Shared.Services;
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
        containerProvider.Resolve<ILanguageManager>().RegisterTextMateLanguage("verilog", "avares://OneWare.Verilog/Assets/verilog.tmLanguage.json", ".v");
        containerProvider.Resolve<ILanguageManager>().RegisterService(typeof(LanguageServiceVerilog),true, ".v", ".sv");
    }
}