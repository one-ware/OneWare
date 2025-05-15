using System.Text.Json.Nodes;
using Avalonia.Platform;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public abstract class FpgaExtensionBase : IFpgaExtension
{
    private readonly ILogger _logger;

    protected FpgaExtensionBase(string name, string connector, ILogger logger)
    {
        Name = name;
        Connector = connector;
        _logger = logger;
    }

    public string Name { get; }
    public string Connector { get; }

    public IList<HardwarePin> Pins { get; } = new List<HardwarePin>();
    public IList<HardwareInterface> Interfaces { get; } = new List<HardwareInterface>();

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
                var interfacePin = pin["interfacePin"]?.ToString();
                var name = pin["name"]?.ToString();

                if (name == null || interfacePin == null) continue;

                Pins.Add(new HardwarePin(name, description, interfacePin));
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

                        if (name == null) throw new Exception("Interface name not defined");
                        if (pinName == null) throw new Exception($"Pin name not found in interface {name}");

                        newInterface.Pins.Add(new HardwareInterfacePin(name, pinName));
                    }

                    Interfaces.Add(newInterface);
                }
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}
