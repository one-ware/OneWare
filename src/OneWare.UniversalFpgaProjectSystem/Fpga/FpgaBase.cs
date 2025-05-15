using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Avalonia.Platform;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public abstract class FpgaBase : IFpga
{
    protected readonly Dictionary<string, string> InternalProperties;
    private readonly ILogger _logger;

    protected FpgaBase(string name, ILogger logger, Dictionary<string, string>? properties = null)
    {
        Name = name;
        _logger = logger;
        InternalProperties = properties ?? [];
        Properties = new ReadOnlyDictionary<string, string>(InternalProperties);
    }

    public string Name { get; }

    public IList<HardwarePin> Pins { get; } = new List<HardwarePin>();

    public IList<HardwareInterface> Interfaces { get; } = new List<HardwareInterface>();

    public IReadOnlyDictionary<string, string> Properties { get; }

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

            foreach (var pin in properties["pins"]?.AsArray() ?? [])
            {
                if (pin == null) continue;

                var description = pin["description"]?.ToString();
                var name = pin["name"]?.ToString();

                if (name == null) continue;

                Pins.Add(new HardwarePin(name, description));
            }

            if (properties["interfaces"]?.AsArray() is { } fpgaInterfaces)
            {
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

                        if (name == null)
                            throw new Exception("Interface name not defined");
                        if (pinName == null)
                            throw new Exception($"Pin name not found in interface {name}");

                        newInterface.Pins.Add(new HardwareInterfacePin(name, pinName));
                    }

                    Interfaces.Add(newInterface);
                }
            }

            if (properties["properties"]?.AsObject() is { } fpgaSettings)
            {
                foreach (var (key, value) in fpgaSettings)
                {
                    var settingName = key;
                    var settingValue = value?.ToString();

                    if (settingValue != null)
                        InternalProperties.Add(settingName, settingValue);
                }
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}
