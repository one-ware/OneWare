using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Markdown.Avalonia.SyntaxHigh;
using OneWare.Core.Extensions.TextMate;
using OneWare.Shared;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Services;
using Prism.Ioc;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace OneWare.Core.Services;

internal class LanguageManager : ObservableObject, ILanguageManager
{
    private readonly Dictionary<string, string> _extensionLinks = new();
    private readonly Dictionary<string, Type> _singleInstanceServerTypes = new();
    private readonly Dictionary<string, Type> _workspaceServerTypes = new();
    private readonly Dictionary<Type, ILanguageService> _singleInstanceServers = new();
    private readonly Dictionary<Type, Dictionary<string, ILanguageService>> _workspaceServers = new();

    private readonly Dictionary<string, Type> _standAloneTypeAssistance = new();

    private readonly CustomTextMateRegistryOptions _textMateRegistryOptions = new();
    public IRegistryOptions RegistryOptions => _textMateRegistryOptions;

    private IRawTheme _currentEditorTheme;

    public IRawTheme CurrentEditorTheme
    {
        get => _currentEditorTheme;
        private set => SetProperty(ref _currentEditorTheme, value);
    }

    public LanguageManager(ISettingsService settingsService)
    {
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
                    SyntaxSetup.CurrentEditorTheme = CurrentEditorTheme;
                });
            });

        //Hoverbox hack
        SyntaxSetup.RegistryOptions = _textMateRegistryOptions;
    }

    public void RegisterTextMateLanguage(string id, string grammarPath, params string[] extensions)
    {
        _textMateRegistryOptions.RegisterLanguage(id, grammarPath, extensions);
    }

    public void RegisterLanguageExtensionLink(string source, string target)
    {
        _extensionLinks.TryAdd(source, target);
        _textMateRegistryOptions.RegisterExtensionLink(source, target);
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
            if (!workspaceDependent) _singleInstanceServerTypes[s] = type;
            else
            {
                _workspaceServerTypes[s] = type;
                _workspaceServers.TryAdd(type, new Dictionary<string, ILanguageService>());
            }
        }
    }

    public void RegisterStandaloneTypeAssistance(Type type, params string[] supportedFileTypes)
    {
        foreach (var s in supportedFileTypes)
        {
            _standAloneTypeAssistance[s] = type;
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
            if (!_standAloneTypeAssistance.TryGetValue(extensionLink ?? editor.CurrentFile.Extension, out var type)) return null;
            if (ContainerLocator.Container.Resolve(type, (typeof(IEditor), editor)) is not ITypeAssistance newInstance)
                throw new TypeLoadException(nameof(type) + " is not " + nameof(ITypeAssistance));
            return newInstance;
        }

        ;
        _ = service.ActivateAsync();
        return service.GetTypeAssistance(editor);
    }

    public void AddProject(IProjectRoot project)
    {
        //AllServers.Add(new LanguageServiceVhdl(project.ProjectPath));
        //AllServers.Add(new LanguageServiceVerilog(project.ProjectPath));
        //AllServers.Add(new LanguageServiceSystemVerilog(project.ProjectPath));
    }

    public void RemoveProject(IProjectRoot project)
    {
        // var remove = _workspaceServers.SelectMany(x => x.Value.Where(x => x.Key == project.RootFolderPath));
        //
        // foreach (var r in remove)
        // {
        //     _workspaceServers.Remove(r.Key);
        //     _ = r.Value.DeactivateAsync();
        // }
    }

    public async Task CleanResourcesAsync()
    {
        await Task.WhenAll(_singleInstanceServers.Select(x => x.Value.DeactivateAsync()));
        await Task.WhenAll(_workspaceServers
            .SelectMany(x => x.Value.Select(b => b.Value.DeactivateAsync())));
    }
}