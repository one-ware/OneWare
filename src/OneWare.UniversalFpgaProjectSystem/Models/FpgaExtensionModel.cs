using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaExtensionModel : ObservableObject
{
    private FpgaInterfaceModel? _parent;

    public FpgaExtensionModel(IFpgaExtension fpgaExtension)
    {
        FpgaExtension = fpgaExtension;
    }

    public IFpgaExtension FpgaExtension { get; }

    public FpgaInterfaceModel? Parent
    {
        get => _parent;
        set => SetProperty(ref _parent, value);
    }
}