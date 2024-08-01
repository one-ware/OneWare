using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.Max1000;

public class Max1000Fpga : FpgaBase
{
    public Max1000Fpga()
    {
        LoadFromJsonAsset("avares://OneWare.Max1000/Assets/Max1000.json");
    }
}