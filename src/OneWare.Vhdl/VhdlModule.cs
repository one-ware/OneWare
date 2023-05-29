using OneWare.Shared.LanguageService;
using OneWare.Shared.Services;
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
        containerProvider.Resolve<ILanguageManager>().RegisterService(typeof(LanguageServiceVhdl),false);
    }
}