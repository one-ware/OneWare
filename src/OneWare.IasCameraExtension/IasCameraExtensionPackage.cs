using OneWare.IasCameraExtension.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.IasCameraExtension;

public class IasCameraExtensionPackage : IFpgaExtensionPackage
{
    public string Name => "IAS Camera Extension";
    public string Connector => "CruviHS";
    
    public IFpgaExtension LoadExtension()
    {
        return new GenericFpgaExtension(Name, Connector, "avares://OneWare.IasCameraExtension/Assets/IasCameraExtension.json");
    }

    public FpgaExtensionViewModelBase? LoadExtensionViewModel(FpgaExtensionModel fpgaExtensionModel)
    {
        return new IasCameraExtensionViewModel(fpgaExtensionModel);
    }
}