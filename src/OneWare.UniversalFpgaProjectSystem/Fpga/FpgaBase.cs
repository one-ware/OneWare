using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Avalonia.Platform;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public abstract class FpgaBase : IFpga
{
    public string Name { get; private set; } = "Unknown";

    public IList<FpgaPin> Pins { get; } = new List<FpgaPin>();
    
    protected void LoadFromJson(string path)
    {
        var stream = AssetLoader.Open(new Uri(path));
        
        var properties = JsonNode.Parse(stream);
        
        Name = properties?["Name"]?.ToString() ?? "Unknown";

        foreach (var jsonNode in properties["Pins"].AsArray())
        {
            var description = jsonNode["Description"].ToString();
            var name = jsonNode["Name"].ToString();
            
            Pins.Add(new FpgaPin(name, description));
        }
    }
}