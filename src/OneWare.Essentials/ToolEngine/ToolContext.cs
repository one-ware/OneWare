using OneWare.Essentials.Services;

namespace OneWare.Essentials.ToolEngine;

public class ToolContext(string name, string description, string key, List<string>? toolNames = null,
    IReadOnlyList<string>? preferredStrategyKeys = null,
    IReadOnlyDictionary<string, string>? strategyConfiguration = null)
{
    public string Name { get; init; } = name;
    public string Description { get; init; } = description;
    public string Key { get; init; } = key;

    public List<string> ToolNames { get; init; } = toolNames ?? [];

    /// <summary>
    /// Ordered, most-preferred-first list of execution strategy keys this tool would like to use if available.
    /// Referenced purely by string key so the owning plugin never needs a compile-time reference to an
    /// optional strategy provider (e.g. a Docker extension) that may not be installed.
    /// </summary>
    public IReadOnlyList<string> PreferredStrategyKeys { get; init; } = preferredStrategyKeys ?? [];

    /// <summary>
    /// Default strategy-specific configuration for this tool, as opaque string key/value pairs
    /// (e.g. "docker.image" -> "myimage:tag"). Kept as plain strings - rather than a strategy-specific
    /// options type - so the owning plugin never needs a compile-time reference to a strategy it doesn't
    /// know is installed (e.g. a Docker extension). Each strategy reads only the keys it recognizes by
    /// convention and ignores the rest. Retrieve the effective, user-overridable value via
    /// <see cref="IToolService.GetStrategyConfiguration"/> rather than reading this property directly.
    /// </summary>
    public IReadOnlyDictionary<string, string> StrategyConfiguration { get; init; } =
        strategyConfiguration ?? new Dictionary<string, string>();
}