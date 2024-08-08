using OneWare.Max10.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.Max10;

public class Max10UltraFpgaPackage : IFpgaPackage
{
    public string Name => "Core MAX10 Ultra";
    
    public IFpga LoadFpga()
    {
        return new GenericFpga(Name, "avares://OneWare.Max10/Assets/Max10.json", ("QuartusToolchain_Device", "10M16SAU169C8G"));
    }

    public FpgaViewModelBase? LoadFpgaViewModel(FpgaModel fpgaModel)
    {
        return new Max10FpgaViewModel(fpgaModel);
    }
}