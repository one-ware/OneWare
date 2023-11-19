using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.IceBreaker;

public class IceBreakerFpga : FpgaBase
{
    public IceBreakerFpga()
    {
        LoadFromJson("avares://OneWare.IceBreaker/Assets/IceBreakerV1.0e.json");
    }
}