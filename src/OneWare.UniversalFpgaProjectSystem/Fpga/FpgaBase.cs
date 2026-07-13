using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public abstract class FpgaBase : IFpga
{
    protected readonly Dictionary<string, string> InternalProperties;
    private readonly List<PinPropertyDefinition> _allowedPinProperties = [];
    private Dictionary<string, string> _defaultPinProperties = [];

    protected FpgaBase(string name, Dictionary<string, string>? properties = null)
    {
        Name = name;
        InternalProperties = properties ?? [];
        Properties = new ReadOnlyDictionary<string, string>(InternalProperties);
    }

    public string Name { get; }

    public IList<HardwarePin> Pins { get; } = new List<HardwarePin>();

    public IList<HardwareInterface> Interfaces { get; } = new List<HardwareInterface>();

    public IReadOnlyDictionary<string, string> Properties { get; }

    /// <inheritdoc />
    public IReadOnlyList<PinPropertyDefinition> AllowedPinProperties => _allowedPinProperties;

    protected void LoadFromJsonAsset(string path)
    {
        using var stream = AssetLoader.Open(new Uri(path));
        using var reader = new StreamReader(stream);
        LoadFromJson(reader.ReadToEnd());
    }

    protected void LoadFromJsonFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        LoadFromJson(reader.ReadToEnd());
    }

    private void LoadFromJson(string json)
    {
        try
        {
            var properties = JsonNode.Parse(json);

            if (properties == null) return;

            // ── allowedPinProperties ─────────────────────────────────────────────
            if (properties["allowedPinProperties"]?.AsObject() is { } allowedProps)
            {
                foreach (var (key, node) in allowedProps)
                {
                    var values = node?.AsArray()
                        .Select(v => v?.ToString() ?? string.Empty)
                        .ToArray() ?? [];
                    _allowedPinProperties.Add(new PinPropertyDefinition(
                        key,
                        KeyToDisplayName(key),
                        PinPropertyType.ComboBox,
                        values));
                }
            }

            // ── defaultPinProperties ─────────────────────────────────────────────
            if (properties["defaultPinProperties"]?.AsObject() is { } defaultProps)
            {
                _defaultPinProperties = new Dictionary<string, string>();
                foreach (var (k, v) in defaultProps)
                    if (v != null) _defaultPinProperties[k] = v.ToString();
            }

            foreach (var pin in properties["pins"]?.AsArray() ?? [])
            {
                if (pin == null) continue;

                var description = pin["description"]?.ToString();
                var name = pin["name"]?.ToString();

                if (name == null) continue;

                // Merge defaultPinProperties with per-pin "properties" (pin-level wins)
                Dictionary<string, string>? pinProperties = null;
                if (_defaultPinProperties.Count > 0 || pin["properties"]?.AsObject() is { })
                {
                    pinProperties = new Dictionary<string, string>(_defaultPinProperties);
                    if (pin["properties"]?.AsObject() is { } pinProps)
                        foreach (var (k, v) in pinProps)
                            if (v != null) pinProperties[k] = v.ToString();
                }

                Pins.Add(new HardwarePin(name, description, properties: pinProperties));
            }

            if (properties["interfaces"]?.AsArray() is { } fpgaInterfaces)
                foreach (var fpgaInterface in fpgaInterfaces)
                {
                    if (fpgaInterface == null) continue;
                    var interfaceName = fpgaInterface["name"]?.ToString();

                    if (interfaceName == null) continue;

                    var connectorName = fpgaInterface["connector"]?.ToString();
                    var newInterface = new HardwareInterface(interfaceName, connectorName);

                    foreach (var pin in fpgaInterface["pins"]?.AsArray() ?? [])
                    {
                        if (pin == null) continue;

                        var name = pin["name"]?.ToString();
                        var pinName = pin["pin"]?.ToString();

                        if (name == null) throw new Exception("interface name not defined");
                        if (pinName == null) throw new Exception($"pinname not found in interface {name}");

                        newInterface.Pins.Add(new HardwareInterfacePin(name, pinName));
                    }

                    Interfaces.Add(newInterface);
                }

            if (properties["properties"]?.AsObject() is { } fpgaSettings)
                foreach (var (key, value) in fpgaSettings)
                {
                    var settingName = key;
                    var settingValue = value!.ToString();

                    InternalProperties.Add(settingName, settingValue);
                }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    /// <summary>Converts a SCREAMING_SNAKE_CASE key to a human-readable display name.</summary>
    private static string KeyToDisplayName(string key) =>
        string.Join(" ", key.Split('_')
            .Select(w => w.Length == 0 ? w
                : w.Length <= 2 ? w.ToUpperInvariant()          // keep short acronyms uppercase (IO, V)
                : char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
}

