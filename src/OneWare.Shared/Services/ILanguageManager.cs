using System.ComponentModel;
using OneWare.Shared.LanguageService;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace OneWare.Shared.Services;

public interface ILanguageManager : INotifyPropertyChanged
{
    public IRawTheme CurrentEditorTheme { get; }
    public IRegistryOptions RegistryOptions { get; }
    public void RegisterTextMateLanguage(string id, string grammarPath, params string[] extensions);
    public void RegisterLanguageExtensionLink(string source, string target);
    public string? GetTextMateScopeByExtension(string fileExtension);
    public void RegisterService(Type type, bool workspaceDependent, params string[] supportedFileTypes);
    public void RegisterStandaloneTypeAssistance(Type type, params string[] supportedFileTypes);
    public ILanguageService? GetLanguageService(IFile file);
    public ITypeAssistance? GetTypeAssistance(IEditor editor);
    public void AddProject(IProjectRoot project);
    public void RemoveProject(IProjectRoot project);
}