using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Autofac.Features.OwnedInstances; // Add this using for Owned<T>
using IFile = OneWare.Essentials.Models.IFile;

namespace OneWare.Core.Services;

internal class LanguageManager : ObservableObject, ILanguageManager
{
    private readonly Dictionary<string, string> _extensionLinks = new();
    private readonly Dictionary<Type, ILanguageService> _singleInstanceServers = new();
    private readonly Dictionary<string, Type> _singleInstanceServerTypes = new();

    private readonly Dictionary<string, Type> _standAloneTypeAssistance = new();

    private readonly CustomTextMateRegistryOptions _textMateRegistryOptions = new();
    private readonly Dictionary<Type, Dictionary<string, ILanguageService>> _workspaceServers = new(); // Keep ILanguageService here, we'll manage Owned<>
    private readonly Dictionary<string, Owned<ILanguageService>> _activeWorkspaceOwnedInstances = new(); // To keep track of Owned instances for disposal
    private readonly Dictionary<string, Type> _workspaceServerTypes = new();

    private IRawTheme _currentEditorTheme;

    // NEW: Injected factories
    private readonly Func<Type, ILanguageService> _singleInstanceServiceFactory;
    private readonly Func<Type, string, Owned<ILanguageService>> _workspaceServiceFactory;
    private readonly Func<Type, IEditor, ITypeAssistance> _standAloneTypeAssistanceFactory;


    public LanguageManager(
        ISettingsService settingsService,
        // NEW: Inject the factories
        Func<Type, ILanguageService> singleInstanceServiceFactory,
        Func<Type, string, Owned<ILanguageService>> workspaceServiceFactory,
        Func<Type, IEditor, ITypeAssistance> standAloneTypeAssistanceFactory)
    {
        _singleInstanceServiceFactory = singleInstanceServiceFactory ?? throw new ArgumentNullException(nameof(singleInstanceServiceFactory));
        _workspaceServiceFactory = workspaceServiceFactory ?? throw new ArgumentNullException(nameof(workspaceServiceFactory));
        _standAloneTypeAssistanceFactory = standAloneTypeAssistanceFactory ?? throw new ArgumentNullException(nameof(standAloneTypeAssistanceFactory));

        _currentEditorTheme = _textMateRegistryOptions.GetDefaultTheme();

        IDisposable? sub = null;
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
        var effectiveExtension = extensionLink ?? file.Extension;

        if (_workspaceServerTypes.TryGetValue(effectiveExtension, out var type2))
        {
            var workspace = (file is IProjectFile pf ? pf.Root.RootFolderPath : Path.GetDirectoryName(file.FullPath)) ??
                            "";

            if (_workspaceServers[type2].TryGetValue(workspace, out var service2)) return service2;

            // Use the injected factory to resolve the workspace-dependent service
            var ownedService = _workspaceServiceFactory(type2, workspace);
            var newInstance = ownedService.Value; // Get the actual instance from Owned<T>

            if (newInstance is not ILanguageService)
                throw new TypeLoadException($"{type2.Name} is not {nameof(ILanguageService)}");

            _workspaceServers[type2].Add(workspace, newInstance);
            _activeWorkspaceOwnedInstances[workspace + type2.Name] = ownedService; // Store the Owned instance
            return newInstance;
        }

        if (_singleInstanceServerTypes.TryGetValue(effectiveExtension, out var type))
        {
            if (_singleInstanceServers.TryGetValue(type, out var service)) return service;

            // Use the injected factory to resolve the single-instance service
            var newInstance = _singleInstanceServiceFactory(type);

            if (newInstance is not ILanguageService) // Already checked by factory return type, but good for defensive programming
                throw new TypeLoadException($"{type.Name} is not {nameof(ILanguageService)}");

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
            var effectiveExtension = extensionLink ?? editor.CurrentFile.Extension;

            if (!_standAloneTypeAssistance.TryGetValue(effectiveExtension, out var type))
                return null;

            // Use the injected factory to resolve the standalone type assistance
            var newInstance = _standAloneTypeAssistanceFactory(type, editor);

            if (newInstance is not ITypeAssistance) // Already checked by factory return type, but good for defensive programming
                throw new TypeLoadException($"{type.Name} is not {nameof(ITypeAssistance)}");
            return newInstance;
        }

        _ = service.ActivateAsync();
        return service.GetTypeAssistance(editor);
    }

    public void AddProject(IProjectRoot project)
    {
        // No direct instantiation here, but if adding a project triggers language service creation,
        // you'd use the factories here too.
    }

    public void RemoveProject(IProjectRoot project)
    {
        // When a project is removed, dispose of its associated workspace language services
        var workspacePath = project.RootFolderPath;
        var servicesToRemove = _activeWorkspaceOwnedInstances.Where(kv => kv.Key.StartsWith(workspacePath)).ToList();

        foreach (var kv in servicesToRemove)
        {
            // Deactivate and dispose the service
            kv.Value.Value.DeactivateAsync().Wait(); // Consider async/await pattern here
            kv.Value.Dispose(); // Dispose the Owned<T> instance
            _activeWorkspaceOwnedInstances.Remove(kv.Key);

            // Also remove from _workspaceServers dictionary
            var typeName = kv.Key.Substring(workspacePath.Length);
            var matchingType = _workspaceServerTypes.FirstOrDefault(t => t.Value.Name == typeName).Value; // This is a bit hacky, better if key was (workspace, Type)
            if (matchingType != null && _workspaceServers.TryGetValue(matchingType, out var servicesForType))
            {
                servicesForType.Remove(workspacePath);
            }
        }
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
        // Deactivate all single-instance services
        await Task.WhenAll(_singleInstanceServers.Select(x => x.Value.DeactivateAsync()));

        // Deactivate and dispose all active workspace services
        await Task.WhenAll(_activeWorkspaceOwnedInstances.Select(async x =>
        {
            await x.Value.Value.DeactivateAsync();
            x.Value.Dispose(); // Dispose the Owned<T> instance
        }));
        _activeWorkspaceOwnedInstances.Clear(); // Clear the tracking dictionary
        _workspaceServers.Clear(); // Also clear the references to the now-disposed services
    }
}