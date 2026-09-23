using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Python;

public sealed class PythonInterpreterService : IDisposable
{
    public const string GlobalInterpreterSettingKey = "PythonModule_DefaultInterpreter";
    public const string WorkspaceInterpretersSettingKey = "PythonModule_WorkspaceInterpreters";
    private const int MaximumPathDirectories = 128;
    private const int MaximumCandidates = 32;
    private readonly ISettingsService _settings;
    private readonly bool _isWindows;
    private readonly Func<string, bool> _isExecutable;
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly StringComparer _pathComparer;
    private readonly ILogger<PythonInterpreterService> _logger;
    private readonly HashSet<string> _reportedInvalidEntries = [];
    private readonly Dictionary<string, PythonInterpreterResolution> _tracked;
    private readonly CompositeDisposable _subscriptions = new();
    private readonly object _gate = new();
    private bool _initialized;

    public PythonInterpreterService(ISettingsService settings, ILogger<PythonInterpreterService>? logger = null)
        : this(settings, OperatingSystem.IsWindows(), IsExecutableFile, Environment.GetEnvironmentVariable, logger)
    {
    }

    internal PythonInterpreterService(ISettingsService settings, bool isWindows,
        Func<string, bool> isExecutable, Func<string, string?> getEnvironmentVariable,
        ILogger<PythonInterpreterService>? logger = null)
    {
        _settings = settings;
        _isWindows = isWindows;
        _isExecutable = isExecutable;
        _getEnvironmentVariable = getEnvironmentVariable;
        _logger = logger ?? NullLogger<PythonInterpreterService>.Instance;
        _pathComparer = isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _tracked = new Dictionary<string, PythonInterpreterResolution>(_pathComparer);
    }

    public event EventHandler<PythonInterpreterChangedEventArgs>? InterpreterChanged;

    public void Initialize()
    {
        lock (_gate)
        {
            if (_initialized) return;
            if (!_settings.HasSetting(GlobalInterpreterSettingKey))
                _settings.RegisterSetting("Languages", "Python", GlobalInterpreterSettingKey,
                    new FilePathSetting("Default Python interpreter", "", "Automatic", null,
                        path => string.IsNullOrWhiteSpace(path) || IsValidExplicitPath(path),
                        FilePickerFileTypes.All)
                    {
                        HoverDescription = "Absolute path to a Python executable. Empty uses Python on PATH. " +
                                           "Workspace selections and .venv/venv take precedence.",
                        MarkdownDocumentation = "Python must be installed externally. This does not install a runtime " +
                                                "or create an environment. Pyrefly project configuration may override " +
                                                "the editor's interpreter fallback."
                    });
            if (!_settings.HasSetting(WorkspaceInterpretersSettingKey))
                _settings.Register(WorkspaceInterpretersSettingKey, new Dictionary<string, string>());
            _initialized = true;
            _subscriptions.Add(_settings.GetSettingObservable<string>(GlobalInterpreterSettingKey)
                .Skip(1).Subscribe(_ => RefreshTracked()));
            _subscriptions.Add(_settings.GetSettingObservable<Dictionary<string, string>>(WorkspaceInterpretersSettingKey)
                .Skip(1).Subscribe(_ => RefreshTracked()));
        }
    }

    public PythonInterpreterResolution GetInterpreter(string workspace)
    {
        Initialize();
        var normalized = NormalizeWorkspace(workspace);
        lock (_gate)
        {
            var resolution = Resolve(normalized);
            _tracked[normalized] = resolution;
            return resolution;
        }
    }

    public string? GetWorkspaceInterpreter(string workspace)
    {
        Initialize();
        lock (_gate)
            return GetWorkspaceSelections().GetValueOrDefault(NormalizeWorkspace(workspace));
    }

    public void SetWorkspaceInterpreter(string workspace, string? executablePath)
    {
        Initialize();
        var normalized = NormalizeWorkspace(workspace);
        lock (_gate)
        {
            var selections = GetWorkspaceSelections();
            if (string.IsNullOrWhiteSpace(executablePath))
                selections.Remove(normalized);
            else
                selections[normalized] = NormalizeExplicitPath(executablePath);
            // Replace the map, rather than mutating the setting's value, to notify and persist normally.
            _settings.SetSettingValue(WorkspaceInterpretersSettingKey, selections);
        }
    }

    public void Refresh(string workspace)
    {
        Initialize();
        RefreshTracked(NormalizeWorkspace(workspace));
    }

    public Task<IReadOnlyList<PythonInterpreterCandidate>> DiscoverCandidatesAsync(string workspace,
        CancellationToken cancellationToken = default)
    {
        Initialize();
        var normalized = NormalizeWorkspace(workspace);
        string? selected;
        string global;
        lock (_gate)
        {
            selected = GetWorkspaceSelections().GetValueOrDefault(normalized);
            global = _settings.GetSettingValue<string>(GlobalInterpreterSettingKey);
        }
        return Task.Run<IReadOnlyList<PythonInterpreterCandidate>>(() =>
        {
            var candidates = new List<PythonInterpreterCandidate>();
            var seen = new HashSet<string>(_pathComparer);
            void Add(string? path, string label)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(path) || candidates.Count >= MaximumCandidates) return;
                path = NormalizeExplicitPath(path);
                if (Path.IsPathFullyQualified(path) && _isExecutable(path) && seen.Add(path))
                    candidates.Add(new PythonInterpreterCandidate(path, label));
            }

            Add(selected, "Workspace selection");
            Add(GetEnvironmentInterpreterPath(Path.Combine(normalized, ".venv"), _isWindows), "Workspace .venv");
            Add(GetEnvironmentInterpreterPath(Path.Combine(normalized, "venv"), _isWindows), "Workspace venv");
            foreach (var variable in new[] { "VIRTUAL_ENV", "CONDA_PREFIX" })
            {
                var environment = _getEnvironmentVariable(variable);
                if (!string.IsNullOrWhiteSpace(environment) && Path.IsPathFullyQualified(environment))
                    Add(variable == "CONDA_PREFIX" && _isWindows
                        ? Path.Combine(environment, "python.exe")
                        : GetEnvironmentInterpreterPath(environment, _isWindows), variable);
            }
            Add(global, "Global default");
            foreach (var path in GetPathExecutables())
                Add(path, "PATH");
            return candidates;
        }, cancellationToken);
    }

    public static string GetEnvironmentInterpreterPath(string environment, bool isWindows) =>
        isWindows ? Path.Combine(environment, "Scripts", "python.exe") : Path.Combine(environment, "bin", "python");

    public static string NormalizeWorkspace(string workspace) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace));

    private Dictionary<string, string> GetWorkspaceSelections()
    {
        var result = new Dictionary<string, string>(_pathComparer);
        foreach (var entry in _settings.GetSettingValue<Dictionary<string, string>>(WorkspaceInterpretersSettingKey))
        {
            try
            {
                if (!Path.IsPathFullyQualified(entry.Key))
                {
                    ReportInvalidEntry(entry.Key);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(entry.Value))
                    ReportInvalidEntry(entry.Key);
                result[NormalizeWorkspace(entry.Key)] = entry.Value ?? "";
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                ReportInvalidEntry(entry.Key);
            }
        }
        return result;
    }

    private PythonInterpreterResolution Resolve(string workspace)
    {
        if (GetWorkspaceSelections().TryGetValue(workspace, out var selected))
            return ResolveExplicit(workspace, selected, PythonInterpreterSource.WorkspaceOverride);
        foreach (var name in new[] { ".venv", "venv" })
        {
            var path = GetEnvironmentInterpreterPath(Path.Combine(workspace, name), _isWindows);
            if (_isExecutable(path))
                return Resolved(workspace, path, PythonInterpreterSource.WorkspaceEnvironment);
        }
        var global = _settings.GetSettingValue<string>(GlobalInterpreterSettingKey);
        if (!string.IsNullOrWhiteSpace(global))
            return ResolveExplicit(workspace, global, PythonInterpreterSource.GlobalDefault);
        var fallback = GetPathExecutables().FirstOrDefault(_isExecutable);
        return fallback != null
            ? Resolved(workspace, fallback, PythonInterpreterSource.Path)
            : new PythonInterpreterResolution(workspace, null, PythonInterpreterSource.None,
                PythonInterpreterStatus.Missing,
                "No Python interpreter found. Install Python externally or select an existing executable. " +
                "Available Pyrefly analysis remains usable.");
    }

    private PythonInterpreterResolution ResolveExplicit(string workspace, string path, PythonInterpreterSource source)
    {
        path = NormalizeExplicitPath(path);
        return IsValidExplicitPath(path)
            ? Resolved(workspace, path, source)
            : new PythonInterpreterResolution(workspace, path, source,
                PythonInterpreterStatus.InvalidExplicitSelection,
                $"The selected Python interpreter is missing or not executable: {path}. " +
                "Select an existing executable or choose Automatic; no fallback was used.");
    }

    private static PythonInterpreterResolution Resolved(string workspace, string path, PythonInterpreterSource source) =>
        new(workspace, path, source, PythonInterpreterStatus.Resolved, $"{source}: {path}");

    private void ReportInvalidEntry(string workspace)
    {
        if (_reportedInvalidEntries.Add(workspace))
            _logger.LogWarning("Malformed persisted Python interpreter selection for workspace {Workspace}. " +
                               "Choose an existing interpreter or Automatic to repair the selection.", workspace);
    }

    private bool IsValidExplicitPath(string path) =>
        Path.IsPathFullyQualified(path) && _isExecutable(path);

    private static string NormalizeExplicitPath(string path)
    {
        try
        {
            // Do not resolve symbolic links: a venv's executable path identifies its environment.
            return Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : path;
        }
        catch (ArgumentException) { return path; }
        catch (NotSupportedException) { return path; }
        catch (PathTooLongException) { return path; }
    }

    private IEnumerable<string> GetPathExecutables()
    {
        var names = _isWindows ? new[] { "python.exe", "python3.exe" } : new[] { "python3", "python" };
        var directories = (_getEnvironmentVariable("PATH") ?? "")
            .Split(_isWindows ? ';' : ':')
            .Take(MaximumPathDirectories).Select(x => x.Trim().Trim('"'))
            .Where(Path.IsPathFullyQualified).Distinct(_pathComparer).ToArray();
        foreach (var name in names)
        foreach (var directory in directories)
        {
            var path = NormalizeExplicitPath(Path.Combine(directory, name));
            yield return path;
        }
    }

    private void RefreshTracked(string? workspace = null)
    {
        List<PythonInterpreterResolution> changed = [];
        lock (_gate)
        {
            foreach (var key in _tracked.Keys.ToArray())
            {
                if (workspace != null && !_pathComparer.Equals(key, workspace)) continue;
                var previous = _tracked[key];
                var current = Resolve(key);
                _tracked[key] = current;
                if (previous.Status != current.Status || previous.Source != current.Source ||
                    !_pathComparer.Equals(previous.ExecutablePath, current.ExecutablePath))
                    changed.Add(current);
            }
        }
        foreach (var resolution in changed)
            InterpreterChanged?.Invoke(this, new PythonInterpreterChangedEventArgs(resolution));
    }

    private static bool IsExecutableFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            if (OperatingSystem.IsWindows())
                return string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);
            return (File.GetUnixFileMode(path) &
                    (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }

    public void Dispose() => _subscriptions.Dispose();
}
