using OneWare.Essentials.Services;
using Prism.Modularity;

namespace OneWare.Json;

public class JsonModule 
{
    private readonly ILanguageManager _languageManager;

    public JsonModule(ILanguageManager languageManager)
    {
        _languageManager = languageManager;
    }
    public void OnInitialized()
    {
        _languageManager.RegisterStandaloneTypeAssistance(typeof(TypeAssistanceJson), ".json");
    }
}