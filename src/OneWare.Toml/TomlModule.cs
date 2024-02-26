using OneWare.Essentials.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Toml;

public class TomlModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<ILanguageManager>().RegisterStandaloneTypeAssistance(typeof(TypeAssistanceToml), ".toml");
        containerProvider.Resolve<ILanguageManager>().RegisterTextMateLanguage("toml", "avares://OneWare.Toml/Assets/TOML.tmLanguage.json", ".toml");
    }
}