using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public sealed class FpgaModel : ObservableObject, IHardwareModel
{
    private string _searchTextNodes = string.Empty;

    private string _searchTextPins = string.Empty;

    private FpgaNodeModel? _selectedNodeModel;

    private HardwarePinModel? _selectedPinModel;

    private ExtensionModel? _selectedExtensionModel;

    public FpgaModel(IFpga fpga)
    {
        Fpga = fpga;

        foreach (var pin in fpga.Pins) AddPin(pin);

        foreach (var fpgaInterface in fpga.Interfaces) AddInterface(fpgaInterface);

        ConnectCommand = new RelayCommand(ConnectSelected, () => SelectedNodeModel is { ConnectedPin: null }
                                                                 && SelectedPinModel is { ConnectedNode : null });

        DisconnectCommand = new RelayCommand(DisconnectSelected, () => SelectedPinModel is { ConnectedNode: not null } || SelectedExtensionModel != null);

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
        
        this.WhenValueChanged(x => x.SelectedExtensionModel).Subscribe(_ =>
        {
            DisconnectCommand.NotifyCanExecuteChanged();
        });

        this.WhenValueChanged(x => x.SearchTextPins).Subscribe(SearchPins);
        this.WhenValueChanged(x => x.SearchTextNodes).Subscribe(SearchNodes);
    }

    public IFpga Fpga { get; }

    public Dictionary<string, HardwarePinModel> PinModels { get; } = new();
    public ObservableCollection<HardwarePinModel> VisiblePinModels { get; } = new();
    public Dictionary<string, FpgaNodeModel> NodeModels { get; } = new();
    public ObservableCollection<FpgaNodeModel> VisibleNodeModels { get; } = new();
    public Dictionary<string, HardwareInterfaceModel> InterfaceModels { get; } = new();

    public HardwarePinModel? SelectedPinModel
    {
        get => _selectedPinModel;
        set
        {
            if (_selectedPinModel != null) _selectedPinModel.IsSelected = false;
            SetProperty(ref _selectedPinModel, value);
            if (_selectedPinModel != null)
            {
                _selectedPinModel.IsSelected = true;
                SelectedExtensionModel = null;
            }
        }
    }

    public FpgaNodeModel? SelectedNodeModel
    {
        get => _selectedNodeModel;
        set => SetProperty(ref _selectedNodeModel, value);
    }
    
    public ExtensionModel? SelectedExtensionModel
    {
        get => _selectedExtensionModel;
        set
        {
            if(_selectedExtensionModel != null) _selectedExtensionModel.IsSelected = false;
            SetProperty(ref _selectedExtensionModel, value);
            if (_selectedExtensionModel != null)
            {
                _selectedExtensionModel.IsSelected = true;
                SelectedPinModel = null;
            }
        }
    }

    public RelayCommand ConnectCommand { get; }

    public RelayCommand DisconnectCommand { get; }

    public string SearchTextPins
    {
        get => _searchTextPins;
        set => SetProperty(ref _searchTextPins, value);
    }

    public string SearchTextNodes
    {
        get => _searchTextNodes;
        set => SetProperty(ref _searchTextNodes, value);
    }

    public event EventHandler? NodeConnected;

    public event EventHandler? NodeDisconnected;

    private void SearchPins(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            SelectedPinModel = null;
            return;
        }

        SelectedPinModel = VisiblePinModels.FirstOrDefault(x =>
            x.Pin.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (x.Pin.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private void SearchNodes(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            SelectedNodeModel = null;
            return;
        }

        SelectedNodeModel =
            VisibleNodeModels.FirstOrDefault(x => x.Node.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    public void Connect(HardwarePinModel pin, FpgaNodeModel fpgaNode)
    {
        pin.ConnectedNode = fpgaNode;
        fpgaNode.ConnectedPin = pin;
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        NodeConnected?.Invoke(this, EventArgs.Empty);
    }

    public void Disconnect(HardwarePinModel pin)
    {
        if (pin.ConnectedNode != null) pin.ConnectedNode.ConnectedPin = null;
        pin.ConnectedNode = null;
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        NodeDisconnected?.Invoke(this, EventArgs.Empty);
    }
    
    public void DisconnectExtension(ExtensionModel model)
    {
        model.ParentInterfaceModel!.SetExtension(null);
        DisconnectCommand.NotifyCanExecuteChanged();
    }

    private void ConnectSelected()
    {
        if (SelectedPinModel is null || SelectedNodeModel is null) return;
        Connect(SelectedPinModel, SelectedNodeModel);

        var index = VisibleNodeModels.IndexOf(SelectedNodeModel);
        
        if (index < VisibleNodeModels.Count - 1)
        {
            SelectedNodeModel = VisibleNodeModels[index + 1];
        }
    }

    private void DisconnectSelected()
    {
        if (SelectedPinModel is not null)
            Disconnect(SelectedPinModel);
        else if (SelectedExtensionModel is not null)
            DisconnectExtension(SelectedExtensionModel);
    }

    public void SelectPin(HardwarePinModel pinModel)
    {
        SelectedPinModel = pinModel;
    }
    
    public void ToggleSelectExtension(ExtensionModel extensionModel)
    {
        SelectedExtensionModel = extensionModel == SelectedExtensionModel ? null : extensionModel;
    }

    private void AddPin(HardwarePin pin)
    {
        var model = new HardwarePinModel(pin, this);
        PinModels.Add(pin.Name, model);
        VisiblePinModels.Add(model);
    }

    public void AddNode(FpgaNode node)
    {
        var model = new FpgaNodeModel(node);
        NodeModels.Add(node.Name, model);
        VisibleNodeModels.Add(model);
    }

    private void AddInterface(HardwareInterface fpgaInterface)
    {
        var model = new HardwareInterfaceModel(fpgaInterface, this);
        InterfaceModels.Add(fpgaInterface.Name, model);
    }

    public override string ToString()
    {
        return Fpga.Name;
    }
}