namespace OneWare.Max1000;

public class Max100016KFpga : Max1000Fpga
{
    public Max100016KFpga()
    {
        Name = "Max 1000 16K";
        InternalProperties["QuartusToolchain_Device"] = "10M16SAU169C8G";
    }
}