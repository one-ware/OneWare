namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public static class FpgaLoader
{
    public static IFpga LoadFromPath(string path)
    {
        return new GenericFpga(path);
    }
}