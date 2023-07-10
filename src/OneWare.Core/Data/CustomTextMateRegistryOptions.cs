using TextMateSharp.Grammars;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace OneWare.Core.Data;

public class CustomTextMateRegistryOptions : IRegistryOptions
{
    private readonly RegistryOptions _defaultRegistryOptions = new RegistryOptions(ThemeName.DarkPlus);
    
    public IRawTheme GetTheme(string scopeName)
    {
        return _defaultRegistryOptions.GetTheme(scopeName);
    }

    public IRawGrammar GetGrammar(string scopeName)
    {
        return _defaultRegistryOptions.GetGrammar(scopeName);
    }

    public ICollection<string> GetInjections(string scopeName)
    {
        return _defaultRegistryOptions.GetInjections(scopeName);
    }

    public IRawTheme GetDefaultTheme()
    {
        return _defaultRegistryOptions.GetDefaultTheme();
    }

    public Language? GetLanguageByExtension(string extension)
    {
        return _defaultRegistryOptions.GetLanguageByExtension(extension);
    }

    public string GetScopeByLanguageId(string languageId) => _defaultRegistryOptions.GetScopeByLanguageId(languageId);

    public IRawTheme LoadTheme(ThemeName name) => _defaultRegistryOptions.LoadTheme(name);
}