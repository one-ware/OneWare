using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.Max10;

public class Max10Fpga : FpgaBase
{
    public Max10Fpga()
    {
        LoadFromJsonAsset("avares://OneWare.Max10/Assets/Max10.json");
    }
}