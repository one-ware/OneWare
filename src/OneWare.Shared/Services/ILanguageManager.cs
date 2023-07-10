using AvaloniaEdit.Highlighting;
using OneWare.Shared.LanguageService;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace OneWare.Shared.Services;

public interface ILanguageManager
{
    public IObservable<IRawTheme> CurrentEditorTheme { get; }
    public IRegistryOptions RegistryOptions { get; }
    public void RegisterHighlighting(string path, params string[] supportedFileTypes);
    public IHighlightingDefinition? GetHighlighting(string fileExtension);
    public string? GetTextMateScopeByExtension(string fileExtension);
    public void RegisterService(Type type, bool workspaceDependent, params string[] supportedFileTypes);
    public ILanguageService? GetLanguageService(IFile file);
}