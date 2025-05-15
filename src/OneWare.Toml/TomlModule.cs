using OneWare.Essentials.Services;

namespace OneWare.Toml;

public class TomlModule
{
    private readonly ILanguageManager _languageManager;

    public TomlModule(ILanguageManager languageManager)
    {
        _languageManager = languageManager;
    }

    public void Initialize()
    {
        _languageManager.RegisterStandaloneTypeAssistance(typeof(TypeAssistanceToml), ".toml");
        _languageManager.RegisterTextMateLanguage("toml", "avares://OneWare.Toml/Assets/TOML.tmLanguage.json", ".toml");
    }
}
