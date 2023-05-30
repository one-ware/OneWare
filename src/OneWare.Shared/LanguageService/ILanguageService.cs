using TextMateSharp.Grammars;

namespace OneWare.Shared.LanguageService;

public interface ILanguageService
{
    public bool IsActivated { get; }
    public string? Workspace { get; }
    public string[] SupportedFileExtensions { get; }
    public Language? TextMateLanguage { get; }
    public ITypeAssistance GetTypeAssistance(IEditor editor);
    public Task ActivateAsync();
    public Task DeactivateAsync();
}