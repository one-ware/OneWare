using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.Cyc5000;

public class Cyc5000Fpga : FpgaBase
{
    public Cyc5000Fpga()
    {
        LoadFromJson("avares://OneWare.Cyc5000/Assets/Cyc5000.json");
    }
}