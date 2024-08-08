using OneWare.Max1000.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.Max1000;

public class Max1000FpgaPackage : IFpgaPackage
{
    public string Name => "MAX1000";
    
    public IFpga LoadFpga()
    {
        return new GenericFpga(Name, "avares://OneWare.Max1000/Assets/Max1000.json");
    }

    public FpgaViewModelBase? LoadFpgaViewModel(FpgaModel fpgaModel)
    {
        return new Max1000ViewModel(fpgaModel);
    }
}