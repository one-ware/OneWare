using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaNodeModel : ObservableObject
{
    public FpgaNode FpgaNode { get; }
    
    private FpgaPinModel? _connection;
    public FpgaPinModel? Connection
    {
        get => _connection;
        set => SetProperty(ref _connection, value);
    }

    public FpgaNodeModel(FpgaNode fpgaNode)
    {
        FpgaNode = fpgaNode;
    }
}