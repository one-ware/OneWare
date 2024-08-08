using OneWare.Max1000.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.Max1000;

public class Max100016KFpgaPackage : IFpgaPackage
{
    public string Name => "Max 1000 16K";
    
    public IFpga LoadFpga()
    {
        return new GenericFpga(Name, "avares://OneWare.Max1000/Assets/Max1000.json", ("QuartusToolchain_Device", "10M16SAU169C8G"));
    }

    public FpgaViewModelBase? LoadFpgaViewModel(FpgaModel fpgaModel)
    {
        return new Max1000ViewModel(fpgaModel);
    }
}