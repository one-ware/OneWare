
namespace OneWare.Shared.LanguageService;

public interface ILanguageService
{
    public bool IsActivated { get; }
    public string? Workspace { get; }
    public ITypeAssistance GetTypeAssistance(IEditor editor);
    public Task ActivateAsync();
    public Task DeactivateAsync();
}