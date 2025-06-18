using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Markdown.Avalonia.SyntaxHigh;
using OneWare.Core.Extensions.TextMate;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using IFile = OneWare.Essentials.Models.IFile;
using Path = System.IO.Path; // Ensure Path is resolved correctly

namespace OneWare.Core.Services;

internal class LanguageManager : ObservableObject, ILanguageManager
{
    private readonly Dictionary<string, string> _extensionLinks = new();
    private readonly Dictionary<Type, ILanguageService> _singleInstanceServers = new();
    private readonly Dictionary<string, Type> _singleInstanceServerTypes = new();

    private readonly Dictionary<string, Type> _standAloneTypeAssistance = new();

    private readonly CustomTextMateRegistryOptions _textMateRegistryOptions = new();
    private readonly Dictionary<Type, Dictionary<string, ILanguageService>> _workspaceServers = new();
    private readonly Dictionary<string, Type> _workspaceServerTypes = new();

    private IRawTheme _currentEditorTheme;

    // Inject the new factories
    private readonly ILanguageServiceFactory _languageServiceFactory;
    private readonly ITypeAssistanceFactory _typeAssistanceFactory;
    private readonly ISettingsService _settingsService; // Keep this for theme subscriptions

    public LanguageManager(
        ISettingsService settingsService,
        ILanguageServiceFactory languageServiceFactory, // Inject language service factory
        ITypeAssistanceFactory typeAssistanceFactory // Inject type assistance factory
    )
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _languageServiceFactory = languageServiceFactory ?? throw new ArgumentNullException(nameof(languageServiceFactory));
        _typeAssistanceFactory = typeAssistanceFactory ?? throw new ArgumentNullException(nameof(typeAssistanceFactory));

        _currentEditorTheme = _textMateRegistryOptions.GetDefaultTheme();

        // Subscribe to theme changes using the injected settingsService
        IDisposable? sub = null;
        var generalTheme = _settingsService.GetSettingObservable<string>("General_SelectedTheme")
            .Select(x => x == "Dark"
                ? "Editor_SyntaxTheme_Dark"
                : "Editor_SyntaxTheme_Light")
            .Subscribe(x =>
            {
                sub?.Dispose();
                sub = _settingsService.GetSettingObservable<ThemeName>(x).Subscribe(b =>
                {
                    CurrentEditorTheme = _textMateRegistryOptions.LoadTheme(b);
                    SyntaxOverride.CurrentEditorTheme = CurrentEditorTheme;
                });
            });

        // Hoverbox hack
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
            var workspace = (file is IProjectFile pf ? pf.Root.RootFolderPath : Path.GetDirectoryName(file.FullPath)) ?? "";

            if (_workspaceServers[type2].TryGetValue(workspace, out var service2)) return service2;

            // Use the injected factory here
            ILanguageService newInstance = _languageServiceFactory.CreateWorkspaceDependentService(type2, workspace);

            _workspaceServers[type2].Add(workspace, newInstance);
            return newInstance;
        }

        if (_singleInstanceServerTypes.TryGetValue(extensionLink ?? file.Extension, out var type))
        {
            if (_singleInstanceServers.TryGetValue(type, out var service)) return service;

            // Use the injected factory here
            ILanguageService newInstance = _languageServiceFactory.CreateSingleInstanceService(type);

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

            // Use the injected factory here
            ITypeAssistance newInstance = _typeAssistanceFactory.CreateStandaloneTypeAssistance(type, editor);
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