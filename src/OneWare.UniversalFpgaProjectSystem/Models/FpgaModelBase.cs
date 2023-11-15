using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;

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

    public RelayCommand ConnectCommand { get; }
    
    public RelayCommand DisconnectCommand { get; }

    private string _searchTextPins = string.Empty;

    public string SearchTextPins
    {
        get => _searchTextPins;
        set => SetProperty(ref _searchTextPins, value);
    }
    
    private string _searchTextNodes = string.Empty;

    public string SearchTextNodes
    {
        get => _searchTextNodes;
        set => SetProperty(ref _searchTextNodes, value);
    }

    public event EventHandler NodeConnected;

    public event EventHandler NodeDisconnected;
    
    public FpgaModelBase()
    {
        ConnectCommand = new RelayCommand(ConnectSelected, () => SelectedNode is not null && SelectedPin is not null);
        
        DisconnectCommand = new RelayCommand(DisconnectSelected, () => SelectedPin is {Connection: not null});

        this.WhenValueChanged(x => x.SelectedNode).Subscribe(x =>
        {
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        });
        
        this.WhenValueChanged(x => x.SelectedPin).Subscribe(x =>
        {
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        });

        this.WhenValueChanged(x => x.SearchTextPins).Subscribe(SearchPins);
        this.WhenValueChanged(x => x.SearchTextNodes).Subscribe(SearchNodes);

    }

    private void SearchPins(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            SelectedPin = null;
            return;
        }

        SelectedPin = VisiblePins.FirstOrDefault(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) 
                                                      || x.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private void SearchNodes(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            SelectedNode = null;
            return;
        }

        SelectedNode = VisibleNodes.FirstOrDefault(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    public void Connect(FpgaPinModel pin, NodeModel node)
    {
        pin.Connection = SelectedNode;
        node.Connection = SelectedPin;
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        NodeConnected?.Invoke(this, EventArgs.Empty);
    }

    public void Disconnect(FpgaPinModel pin)
    {
        if (pin.Connection != null) pin.Connection.Connection = null;
        pin.Connection = null;
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        NodeDisconnected?.Invoke(this, EventArgs.Empty);
    }
    
    private void ConnectSelected()
    {
        if (SelectedPin is null || SelectedNode is null) return;
        Connect(SelectedPin, SelectedNode);
    }

    private void DisconnectSelected()
    {
        if (SelectedPin is null) return;
        Disconnect(SelectedPin);
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