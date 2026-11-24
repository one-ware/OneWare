using System.Text.Json;

namespace OneWare.Essentials.PackageManager.Compatibility;

/// <summary>
///     The <c>oneware-plugin.json</c> manifest every plugin ships next to its assemblies.
///     It lists the assemblies the plugin expects from OneWare Studio or from other plugins.
/// </summary>
/// <example>
///     <code>
/// {
///   "manifestVersion": 1,
///   "dependencies": {
///     "OneWare.Essentials": "2027.1.0.0",
///     "OneWare.UniversalFpgaProjectSystem": "2027.1.0.0"
///   }
/// }
///     </code>
/// </example>
public sealed class PluginManifest
{
    public const string FileName = "oneware-plugin.json";

    /// <summary>
    ///     Highest manifest version this version of OneWare Studio understands.
    /// </summary>
    public const int SupportedManifestVersion = 1;

    public PluginManifest(int manifestVersion, IReadOnlyDictionary<string, Version> dependencies)
    {
        ManifestVersion = manifestVersion;
        Dependencies = dependencies;
    }

    public int ManifestVersion { get; }

    /// <summary>
    ///     Assembly names and the versions the plugin was built against.
    /// </summary>
    public IReadOnlyDictionary<string, Version> Dependencies { get; }

    /// <summary>
    ///     Parses the content of an <c>oneware-plugin.json</c>.
    /// </summary>
    /// <exception cref="FormatException">The content is not a valid plugin manifest.</exception>
    public static PluginManifest Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("The plugin manifest must be a JSON object.");

            if (!TryGetProperty(root, "manifestVersion", out var versionElement) ||
                !versionElement.TryGetInt32(out var manifestVersion) || manifestVersion < 1)
                throw new FormatException("The plugin manifest needs a positive \"manifestVersion\".");

            var dependencies = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);
            if (TryGetProperty(root, "dependencies", out var dependenciesElement))
            {
                if (dependenciesElement.ValueKind != JsonValueKind.Object)
                    throw new FormatException("\"dependencies\" must be an object of assembly names and versions.");

                foreach (var dependency in dependenciesElement.EnumerateObject())
                {
                    if (string.IsNullOrWhiteSpace(dependency.Name))
                        throw new FormatException("Dependency names must not be empty.");

                    if (dependency.Value.ValueKind != JsonValueKind.String ||
                        !TryParseVersion(dependency.Value.GetString(), out var version))
                        throw new FormatException($"Dependency \"{dependency.Name}\" has no valid version.");

                    dependencies[dependency.Name.Trim()] = version;
                }
            }

            return new PluginManifest(manifestVersion, dependencies);
        }
        catch (JsonException e)
        {
            throw new FormatException($"The plugin manifest is not valid JSON: {e.Message}", e);
        }
    }

    public static bool TryParse(string? json, out PluginManifest? manifest)
    {
        manifest = null;
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            manifest = Parse(json);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Reads the manifest of the plugin folder <paramref name="pluginPath" />.
    ///     Returns <c>null</c> if it is missing or invalid.
    /// </summary>
    public static PluginManifest? Load(string pluginPath)
    {
        try
        {
            var file = Path.Combine(pluginPath, FileName);
            return File.Exists(file) && TryParse(File.ReadAllText(file), out var manifest) ? manifest : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Parses versions like <c>2027.1.0</c>, <c>2027.1.0.0</c> or <c>1.2.0-beta.1</c> and fills missing parts with 0.
    /// </summary>
    internal static bool TryParseVersion(string? value, out Version version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var numeric = value.Trim().Split('-', '+')[0];
        if (!Version.TryParse(numeric.Contains('.') ? numeric : numeric + ".0", out var parsed)) return false;

        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
        return true;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }

        value = default;
        return false;
    }
}
