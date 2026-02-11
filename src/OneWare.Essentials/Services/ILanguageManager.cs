using System.ComponentModel;
using Avalonia.Media;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.ViewModels;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace OneWare.Essentials.Services;

public interface ILanguageManager : INotifyPropertyChanged
{
    /// <summary>
    /// Current editor theme.
    /// </summary>
    public IRawTheme CurrentEditorTheme { get; }
    /// <summary>
    /// Current editor theme colors.
    /// </summary>
    public Dictionary<string, IBrush> CurrentEditorThemeColors { get; }
    /// <summary>
    /// TextMate registry options.
    /// </summary>
    public IRegistryOptions RegistryOptions { get; }
    /// <summary>
    /// Fired when language support is added for an extension.
    /// </summary>
    public event EventHandler<string>? LanguageSupportAdded;
    /// <summary>
    /// Registers a TextMate grammar for the given extensions.
    /// </summary>
    public void RegisterTextMateLanguage(string id, string grammarPath, params string[] extensions);
    /// <summary>
    /// Maps one extension to another for language support.
    /// </summary>
    public void RegisterLanguageExtensionLink(string source, string target);
    /// <summary>
    /// Returns the TextMate scope for a file extension.
    /// </summary>
    public string? GetTextMateScopeByExtension(string fileExtension);
    /// <summary>
    /// Registers a language service type for given file types.
    /// </summary>
    public void RegisterService(Type type, bool workspaceDependent, params string[] supportedFileTypes);
    /// <summary>
    /// Registers a standalone type assistance for given file types.
    /// </summary>
    public void RegisterStandaloneTypeAssistance(Type type, params string[] supportedFileTypes);
    /// <summary>
    /// Returns the language service for a file path.
    /// </summary>
    public ILanguageService? GetLanguageService(string fullPath);
    /// <summary>
    /// Returns type assistance for an editor.
    /// </summary>
    public ITypeAssistance? GetTypeAssistance(IEditor editor);
}
