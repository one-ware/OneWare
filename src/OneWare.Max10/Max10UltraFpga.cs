namespace OneWare.Max10;

public class Max10UltraFpga : Max10Fpga
{
    public Max10UltraFpga()
    {
        Name = "Core Max10 Ultra";
        InternalProperties["QuartusToolchain_Device"] = "10M16SAU169C8G";
    }
}