using OneWare.Max10.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.Max10;

public class Max10FpgaPackage : IFpgaPackage
{
    public string Name => "Core MAX10";
    
    public IFpga LoadFpga()
    {
        return new GenericFpga(Name, "avares://OneWare.Max10/Assets/Max10.json");
    }

    public FpgaViewModelBase? LoadFpgaViewModel(FpgaModel fpgaModel)
    {
        return new Max10FpgaViewModel(fpgaModel);
    }
}