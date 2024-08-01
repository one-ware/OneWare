using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Avalonia.Platform;

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

    public IList<FpgaPin> Pins { get; } = new List<FpgaPin>();

    public IList<FpgaInterface> Interfaces { get; } = new List<FpgaInterface>();
    
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
        var properties = JsonNode.Parse(json);

        if (properties == null) return;

        foreach (var pin in properties[nameof(Pins)]?.AsArray() ?? [])
        {
            if (pin == null) continue;

            var description = pin["Description"]?.ToString();
            var name = pin["Name"]?.ToString();

            if (name == null) continue;

            Pins.Add(new FpgaPin(name, description));
        }

        if (properties[nameof(Interfaces)]?.AsArray() is { } fpgaInterfaces)
            foreach (var fpgaInterface in fpgaInterfaces)
            {
                if (fpgaInterface == null) continue;
                var interfaceName = fpgaInterface["Name"]?.ToString();

                if (interfaceName == null) continue;

                var connectorName = fpgaInterface["Connector"]?.ToString();
                var newInterface = new FpgaInterface(interfaceName, connectorName);

                foreach (var pin in fpgaInterface["Pins"]?.AsArray() ?? [])
                {
                    if (pin == null) continue;

                    var name = pin["Name"]?.ToString();
                    var pinName = pin["Pin"]?.ToString();

                    var pinObj = Pins.FirstOrDefault(x => x.Name == pinName);

                    if (pinObj == null || name == null) continue;

                    newInterface.Pins.Add(new FpgaInterfacePin(name, pinObj));
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
}