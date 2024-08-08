using OneWare.IceBreaker.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.IceBreaker;

public class IceBreakerFpgaPackage : IFpgaPackage
{
    public string Name => "iCEBreaker V1.0e";
    
    public IFpga LoadFpga()
    {
        return new GenericFpga(Name, "avares://OneWare.IceBreaker/Assets/IceBreakerV1.0e.json");
    }

    public FpgaViewModelBase? LoadFpgaViewModel(FpgaModel fpgaModel)
    {
        return new IceBreakerFpgaViewModel(fpgaModel);
    }
}