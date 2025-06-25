using System.Reactive.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Markdown.Avalonia.SyntaxHigh;
using OneWare.Core.Extensions.TextMate;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using IFile = OneWare.Essentials.Models.IFile;

namespace OneWare.Core.Services;

public class LanguageManager : ObservableObject, ILanguageManager
{
    private readonly Dictionary<string, string> _extensionLinks = new();
    private readonly Dictionary<Type, ILanguageService> _singleInstanceServers = new();
    private readonly Dictionary<string, Type> _singleInstanceServerTypes = new();

    private readonly Dictionary<string, Type> _standAloneTypeAssistance = new();

    private readonly CustomTextMateRegistryOptions _textMateRegistryOptions = new();
    private readonly Dictionary<Type, Dictionary<string, ILanguageService>> _workspaceServers = new();
    private readonly Dictionary<string, Type> _workspaceServerTypes = new();

    private IRawTheme _currentEditorTheme;

    public LanguageManager(ISettingsService settingsService)
    {
        _currentEditorTheme = _textMateRegistryOptions.GetDefaultTheme();

        IDisposable? sub = null;
        try
        {
            var generalTheme = settingsService.GetSettingObservable<string>("General_SelectedTheme")
             .Select(x => x == "Dark"
                 ? "Editor_SyntaxTheme_Dark"
                 : "Editor_SyntaxTheme_Light")
             .Subscribe(x =>
             {
                 sub?.Dispose();
                 sub = settingsService.GetSettingObservable<ThemeName>(x).Subscribe(b =>
                 {
                     CurrentEditorTheme = _textMateRegistryOptions.LoadTheme(b);
                     SyntaxOverride.CurrentEditorTheme = CurrentEditorTheme;
                 });
             });

        }
        catch (Exception)
        {

        }
        //Hoverbox hack
        SyntaxOverride.RegistryOptions = _textMateRegistryOptions;
    }

    public IRegistryOptions RegistryOptions => _textMateRegistryOptions;
    public event EventHandler<string>? LanguageSupportAdded;

    public IRawTheme CurrentEditorTheme
    {
        get => _currentEditorTheme;
        private set
        {
            SetProperty(ref _currentEditorTheme, value);
            UpdateThemeColors();
        }
    }

    public Dictionary<string, IBrush> CurrentEditorThemeColors { get; } = new();

    public void RegisterTextMateLanguage(string id, string grammarPath, params string[] extensions)
    {
        _textMateRegistryOptions.RegisterLanguage(id, grammarPath, extensions);
    }

    public void RegisterLanguageExtensionLink(string source, string target)
    {
        _extensionLinks.TryAdd(source, target);
        _textMateRegistryOptions.RegisterExtensionLink(source, target);

        LanguageSupportAdded?.Invoke(this, source);
    }

    public string? GetTextMateScopeByExtension(string fileExtension)
    {
        var lang = _textMateRegistryOptions.GetLanguageByExtension(fileExtension);
        return lang == null ? null : _textMateRegistryOptions.GetScopeByLanguageId(lang.Id);
    }

    public void RegisterService(Type type, bool workspaceDependent, params string[] supportedFileTypes)
    {
        foreach (var s in supportedFileTypes)
        {
            if (!workspaceDependent)
            {
                _singleInstanceServerTypes[s] = type;
            }
            else
            {
                _workspaceServerTypes[s] = type;
                _workspaceServers.TryAdd(type, new Dictionary<string, ILanguageService>());
            }

            LanguageSupportAdded?.Invoke(this, s);
        }
    }

    public void RegisterStandaloneTypeAssistance(Type type, params string[] supportedFileTypes)
    {
        foreach (var s in supportedFileTypes)
        {
            _standAloneTypeAssistance[s] = type;

            LanguageSupportAdded?.Invoke(this, s);
        }
    }

    public ILanguageService? GetLanguageService(IFile file)
    {
        _extensionLinks.TryGetValue(file.Extension, out var extensionLink);
        if (_workspaceServerTypes.TryGetValue(extensionLink ?? file.Extension, out var type2))
        {
            var workspace = (file is IProjectFile pf ? pf.Root.RootFolderPath : Path.GetDirectoryName(file.FullPath)) ??
                            "";

            if (_workspaceServers[type2].TryGetValue(workspace, out var service2)) return service2;
            if (ContainerLocator.Container.Resolve(type2, (typeof(string), workspace)) is not
                ILanguageService newInstance)
                throw new TypeLoadException(nameof(type2) + " is not " + nameof(ILanguageService));

            _workspaceServers[type2].Add(workspace, newInstance);
            return newInstance;
        }

        if (_singleInstanceServerTypes.TryGetValue(extensionLink ?? file.Extension, out var type))
        {
            if (_singleInstanceServers.TryGetValue(type, out var service)) return service;
            if (ContainerLocator.Container.Resolve(type) is not ILanguageService newInstance)
                throw new TypeLoadException(nameof(type2) + " is not " + nameof(ILanguageService));

            _singleInstanceServers.Add(type, newInstance);
            return newInstance;
        }

        return null;
    }

    public ITypeAssistance? GetTypeAssistance(IEditor editor)
    {
        if (editor.CurrentFile == null) throw new NullReferenceException(nameof(editor.CurrentFile));
        var service = GetLanguageService(editor.CurrentFile);

        if (service == null)
        {
            _extensionLinks.TryGetValue(editor.CurrentFile.Extension, out var extensionLink);
            if (!_standAloneTypeAssistance.TryGetValue(extensionLink ?? editor.CurrentFile.Extension, out var type))
                return null;
            if (ContainerLocator.Container.Resolve(type, (typeof(IEditor), editor)) is not ITypeAssistance newInstance)
                throw new TypeLoadException(nameof(type) + " is not " + nameof(ITypeAssistance));
            return newInstance;
        }

        _ = service.ActivateAsync();
        return service.GetTypeAssistance(editor);
    }

    public void AddProject(IProjectRoot project)
    {
    }

    public void RemoveProject(IProjectRoot project)
    {
    }

    private void UpdateThemeColors()
    {
        CurrentEditorThemeColors.Clear();

        foreach (var tokenColor in CurrentEditorTheme.GetTokenColors())
            if (tokenColor.GetScope() is IList<object> scopes)
                foreach (var scopeObj in scopes)
                {
                    if (scopeObj is not string scope) continue;

                    var kind = scope switch
                    {
                        "entity.name.class" => "class",
                        "entity.name.function" => "function",
                        "entity.name.type" => "type",
                        "entity.name.variable" => "variable",
                        "entity.name.namespace" => "namespace",
                        "entity.name.constant" => "constant",
                        "entity.name.operator" => "operator",
                        _ => null
                    };

                    if (kind != null)
                        CurrentEditorThemeColors[kind] = SolidColorBrush.Parse(tokenColor.GetSetting().GetForeground());
                }
    }

    public async Task CleanResourcesAsync()
    {
        await Task.WhenAll(_singleInstanceServers.Select(x => x.Value.DeactivateAsync()));
        await Task.WhenAll(_workspaceServers
            .SelectMany(x => x.Value.Select(b => b.Value.DeactivateAsync())));
    }
}