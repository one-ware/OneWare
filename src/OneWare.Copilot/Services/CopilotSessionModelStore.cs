using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

internal sealed record CopilotSessionModelState(
    string? ModelId,
    string? ReasoningEffort,
    string? ContextTier,
    string? AutoTier,
    DateTimeOffset UpdatedAt);

internal sealed class CopilotSessionModelStore
{
    private const int MaxEntries = 200;
    private static readonly Dictionary<string, CopilotSessionModelStore> Stores = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object StoresLock = new();

    private readonly string _path;
    private readonly object _sync = new();
    private Dictionary<string, CopilotSessionModelState>? _states;

    private CopilotSessionModelStore(string path)
    {
        _path = path;
    }

    public static CopilotSessionModelStore ForFile(string path)
    {
        lock (StoresLock)
        {
            if (!Stores.TryGetValue(path, out var store))
            {
                store = new CopilotSessionModelStore(path);
                Stores[path] = store;
            }

            return store;
        }
    }

    public CopilotSessionModelState? Get(string sessionId)
    {
        lock (_sync)
        {
            EnsureLoaded();
            return _states!.TryGetValue(sessionId, out var state) ? state : null;
        }
    }

    public void Set(string sessionId, CopilotSessionModelState state)
    {
        lock (_sync)
        {
            EnsureLoaded();
            _states![sessionId] = state with { UpdatedAt = DateTimeOffset.UtcNow };
            Prune();
            Save();
        }
    }

    private void EnsureLoaded()
    {
        if (_states != null) return;

        try
        {
            if (!File.Exists(_path))
            {
                _states = new Dictionary<string, CopilotSessionModelState>(StringComparer.Ordinal);
                return;
            }

            var states = JsonSerializer.Deserialize<Dictionary<string, CopilotSessionModelState>>(
                File.ReadAllText(_path));
            _states = states == null
                ? new Dictionary<string, CopilotSessionModelState>(StringComparer.Ordinal)
                : new Dictionary<string, CopilotSessionModelState>(states, StringComparer.Ordinal);
            Prune();
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex,
                "Could not read Copilot session model state from {Path}.", _path);
            _states = new Dictionary<string, CopilotSessionModelState>(StringComparer.Ordinal);
        }
    }

    private void Prune()
    {
        if (_states == null || _states.Count <= MaxEntries) return;

        foreach (var key in _states
                     .OrderByDescending(x => x.Value.UpdatedAt)
                     .Skip(MaxEntries)
                     .Select(x => x.Key)
                     .ToArray())
            _states.Remove(key);
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_states,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex,
                "Could not write Copilot session model state to {Path}.", _path);
        }
    }
}
