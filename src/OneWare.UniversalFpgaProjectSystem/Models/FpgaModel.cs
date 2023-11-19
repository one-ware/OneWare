using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaModel : ObservableObject
{
    public IFpga Fpga { get; }
    
    public Dictionary<string, FpgaPinModel> PinModels { get; } = new();
    public ObservableCollection<FpgaPinModel> VisiblePinModels { get; } = new();
    public Dictionary<string, FpgaNodeModel> NodeModels { get; } = new();
    public ObservableCollection<FpgaNodeModel> VisibleNodeModels { get; } = new();
    
    
    private FpgaPinModel? _selectedPinModel;
    public FpgaPinModel? SelectedPinModel
    {
        get => _selectedPinModel;
        set => SetProperty(ref _selectedPinModel, value);
    }

    private FpgaNodeModel? _selectedNodeModel;
    public FpgaNodeModel? SelectedNodeModel
    {
        get => _selectedNodeModel;
        set => SetProperty(ref _selectedNodeModel, value);
    }

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

    public event EventHandler? NodeConnected;

    public event EventHandler? NodeDisconnected;

    protected FpgaModel(IFpga fpga)
    {
        Fpga = fpga;

        foreach (var pin in fpga.Pins)
        {
            AddPin(pin);
        }
        
        ConnectCommand = new RelayCommand(ConnectSelected, () => SelectedNodeModel is { Connection: null } 
                                                                 && SelectedPinModel is { Connection :null });
        
        DisconnectCommand = new RelayCommand(DisconnectSelected, () => SelectedPinModel is {Connection: not null});

        this.WhenValueChanged(x => x.SelectedNodeModel).Subscribe(_ =>
        {
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        });
        
        this.WhenValueChanged(x => x.SelectedPinModel).Subscribe(_ =>
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
            SelectedPinModel = null;
            return;
        }

        SelectedPinModel = VisiblePinModels.FirstOrDefault(x => x.Pin.Name.Contains(search, StringComparison.OrdinalIgnoreCase) 
                                                      || (x.Pin.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private void SearchNodes(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            SelectedNodeModel = null;
            return;
        }

        SelectedNodeModel = VisibleNodeModels.FirstOrDefault(x => x.FpgaNode.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    public void Connect(FpgaPinModel pin, FpgaNodeModel fpgaNode)
    {
        pin.Connection = fpgaNode;
        fpgaNode.Connection = pin;
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
        if (SelectedPinModel is null || SelectedNodeModel is null) return;
        Connect(SelectedPinModel, SelectedNodeModel);
    }

    private void DisconnectSelected()
    {
        if (SelectedPinModel is null) return;
        Disconnect(SelectedPinModel);
    }

    public void SelectPin(FpgaPinModel pinModel)
    {
        SelectedPinModel = pinModel;
    }

    private void AddPin(FpgaPin pin)
    {
        var model = new FpgaPinModel(pin, this);
        PinModels.Add(pin.Name, model);
        VisiblePinModels.Add(model);
    }
    
    public void AddNode(FpgaNode node)
    {
        var model = new FpgaNodeModel(node);
        NodeModels.Add(node.Name, model);
        VisibleNodeModels.Add(model);
    }
}