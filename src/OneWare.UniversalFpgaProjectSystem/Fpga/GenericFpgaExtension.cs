namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class GenericFpgaExtension : FpgaExtensionBase
{
    public GenericFpgaExtension(string name, string connector, string jsonPath) : base(name, connector)
    {
        if (jsonPath.StartsWith("avares://"))
            LoadFromJsonAsset(jsonPath);
        else
            LoadFromJsonFile(jsonPath);
    }
}