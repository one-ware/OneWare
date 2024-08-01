using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.IceBreaker;

public class IceBreakerFpga : FpgaBase
{
    public IceBreakerFpga()
    {
        LoadFromJsonAsset("avares://OneWare.IceBreaker/Assets/IceBreakerV1.0e.json");
    }
}