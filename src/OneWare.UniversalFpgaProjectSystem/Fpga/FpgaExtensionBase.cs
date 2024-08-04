using System.Text.Json.Nodes;
using Avalonia.Platform;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class FpgaExtensionBase : IFpgaExtension
{
    public FpgaExtensionBase(string name, string connector)
    {
        Name = name;
        Connector = connector;
    }

    public string Name { get; }
    public string Connector { get; }
    
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
    }
}