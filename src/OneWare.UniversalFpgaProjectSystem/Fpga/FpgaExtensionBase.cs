namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class FpgaExtensionBase : IFpgaExtension
{
    public FpgaExtensionBase(string name, string connector)
    {
        Name = name;
        Connector = connector;
    }

    public string Name { get; }
    public string Connector { get; }
}