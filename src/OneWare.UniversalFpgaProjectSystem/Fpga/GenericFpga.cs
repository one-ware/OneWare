namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class GenericFpga : FpgaBase
{
    public GenericFpga(string jsonPath)
    {
        LoadFromJsonFile(jsonPath);
    }
}