using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaNodeModel : ObservableObject
{
    private HardwarePinModel? _connectedPin;

    public FpgaNodeModel(FpgaNode node)
    {
        Node = node;
    }

    public FpgaNode Node { get; }

    public HardwarePinModel? ConnectedPin
    {
        get => _connectedPin;
        set => SetProperty(ref _connectedPin, value);
    }

    public override string ToString()
    {
        return ConnectedPin is null ? Node.Name : $"{Node.Name} <-> {ConnectedPin.Pin.Name}";
    }
}