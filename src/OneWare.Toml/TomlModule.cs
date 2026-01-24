using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Services;

namespace OneWare.Toml;

public class TomlModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<ILanguageManager>()
            .RegisterStandaloneTypeAssistance(typeof(TypeAssistanceToml), ".toml");
        serviceProvider.Resolve<ILanguageManager>()
            .RegisterTextMateLanguage("toml", "avares://OneWare.Toml/Assets/TOML.tmLanguage.json", ".toml");
    }
}

