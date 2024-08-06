using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Avalonia.Platform;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public abstract class FpgaBase : IFpga
{
    protected readonly Dictionary<string, string> InternalProperties;

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

            foreach (var pin in properties[nameof(Pins)]?.AsArray() ?? [])
            {
                if (pin == null) continue;

                var description = pin["Description"]?.ToString();
                var name = pin["Name"]?.ToString();

                if (name == null) continue;

                Pins.Add(new HardwarePin(name, description));
            }

            if (properties[nameof(Interfaces)]?.AsArray() is { } fpgaInterfaces)
                foreach (var fpgaInterface in fpgaInterfaces)
                {
                    if (fpgaInterface == null) continue;
                    var interfaceName = fpgaInterface["Name"]?.ToString();

                    if (interfaceName == null) continue;

                    var connectorName = fpgaInterface["Connector"]?.ToString();
                    var newInterface = new HardwareInterface(interfaceName, connectorName);

                    foreach (var pin in fpgaInterface["Pins"]?.AsArray() ?? [])
                    {
                        if (pin == null) continue;

                        var name = pin["Name"]?.ToString();
                        var pinName = pin["Pin"]?.ToString();
                        
                        if (name == null) throw new Exception($"interface name not defined");
                        if (pinName == null) throw new Exception($"pinname not found in interface {name}");

                        newInterface.Pins.Add(new HardwareInterfacePin(name, pinName));
                    }

                    Interfaces.Add(newInterface);
                }

            if (properties[nameof(Properties)]?.AsObject() is { } fpgaSettings)
                foreach (var (key, value) in fpgaSettings)
                {
                    var settingName = key;
                    var settingValue = value!.ToString();

                    InternalProperties.Add(settingName, settingValue);
                }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message,e);
        }
    }
}