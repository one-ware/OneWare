using AvaloniaEdit.Highlighting;
using OneWare.Shared.LanguageService;

namespace OneWare.Shared.Services;

public interface ILanguageManager
{
    public void RegisterHighlighting(string path, params string[] supportedFileTypes);
    public IHighlightingDefinition? GetHighlighting(string fileExtension);
    public void RegisterService(Type type, bool workspaceDependent, params string[] supportedFileTypes);
    public ILanguageService? GetLanguageService(IFile file);
}