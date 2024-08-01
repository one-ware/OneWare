using OneWare.Cyc5000.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.Cyc5000;

public class Cyc5000FpgaPackage : IFpgaPackage
{
    public string Name => "CYC5000";
    
    public IFpga LoadFpga()
    {
        return new GenericFpga(Name, "avares://OneWare.Cyc5000/Assets/Cyc5000.json");
    }

    public FpgaViewModelBase? LoadFpgaViewModel(FpgaModel fpgaModel)
    {
        return new Cyc5000ViewModel(fpgaModel);
    }
}