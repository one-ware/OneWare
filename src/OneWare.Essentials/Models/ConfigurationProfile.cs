using System.Text.Json.Serialization;

namespace OneWare.Essentials.Models;

/// <summary>
/// Represents an exported IDE configuration profile that captures settings, installed packages,
/// and package sources for easy replication across installations.
/// </summary>
public class ConfigurationProfile
{
    /// <summary>
    /// Schema version for forward compatibility.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Optional human-readable name for this profile.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Optional description of the profile.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Timestamp when the profile was exported.
    /// </summary>
    [JsonPropertyName("exportedAt")]
    public DateTimeOffset ExportedAt { get; set; }

    /// <summary>
    /// IDE settings as key-value pairs.
    /// </summary>
    [JsonPropertyName("settings")]
    public Dictionary<string, object?> Settings { get; set; } = new();

    /// <summary>
    /// List of packages that should be installed.
    /// </summary>
    [JsonPropertyName("packages")]
    public List<ConfigurationProfilePackage> Packages { get; set; } = [];

    /// <summary>
    /// Custom package repository URLs.
    /// </summary>
    [JsonPropertyName("packageSources")]
    public List<string> PackageSources { get; set; } = [];
}

/// <summary>
/// Represents a package entry in a configuration profile.
/// </summary>
public class ConfigurationProfilePackage
{
    /// <summary>
    /// The package identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The installed version (null means latest).
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }
}
