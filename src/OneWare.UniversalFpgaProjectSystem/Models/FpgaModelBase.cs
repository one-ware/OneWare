using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public abstract class FpgaModelBase : ObservableObject
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true
    };
    
    public Dictionary<string, FpgaPinModel> Pins { get; } = new();
    public ObservableCollection<FpgaPinModel> VisiblePins { get; } = new();

    public Dictionary<string, NodeModel> Nodes { get; } = new();
    public ObservableCollection<NodeModel> VisibleNodes { get; } = new();
    
    
    private FpgaPinModel? _selectedPin;
    public FpgaPinModel? SelectedPin
    {
        get => _selectedPin;
        set => SetProperty(ref _selectedPin, value);
    }

    private NodeModel? _selectedNode;
    public NodeModel? SelectedNode
    {
        get => _selectedNode;
        set => SetProperty(ref _selectedNode, value);
    }

    public string Name { get; private set; } = "Unknown";

    protected FpgaModelBase()
    {
        
    }

    protected void LoadFromJson(string path)
    {
        var stream = AssetLoader.Open(new Uri(path));
        
        var properties = JsonSerializer.Deserialize<JsonObject>(stream, SerializerOptions);
        
        Name = properties?["Name"]?.ToString() ?? "Unknown";

        foreach (var jsonNode in properties["Pins"].AsArray())
        {
            var description = jsonNode["Description"].ToString();
            var name = jsonNode["Name"].ToString();

            var pin = new FpgaPinModel(name, description, this);
            Pins.Add(name, pin);
            VisiblePins.Add(pin);
        }
    }

    public void SelectPin(FpgaPinModel model)
    {
        SelectedPin = model;
    }
}