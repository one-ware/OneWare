using OneWare.Shared.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Json;

public class JsonModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<ILanguageManager>().RegisterStandaloneTypeAssistance(typeof(TypeAssistanceJson), ".json", ".fpgaproj", ".vcdconf");
    }
}