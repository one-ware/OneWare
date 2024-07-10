using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaExtensionModel : ObservableObject
{
    public IFpgaExtension FpgaExtension { get; }

    private FpgaInterfaceModel? _parent;
    public FpgaInterfaceModel? Parent
    {
        get => _parent;
        set => SetProperty(ref _parent, value);
    }

    public FpgaExtensionModel(IFpgaExtension fpgaExtension)
    {
        FpgaExtension = fpgaExtension;
    }
}