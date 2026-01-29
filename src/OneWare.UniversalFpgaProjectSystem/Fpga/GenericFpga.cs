namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class GenericFpga : FpgaBase
{
    public GenericFpga(string name, string jsonPath, params (string key, string value)[] propertyOverwrite) : base(name)
    {
        if (jsonPath.StartsWith("avares://"))
            LoadFromJsonAsset(jsonPath);
        else
            LoadFromJsonFile(jsonPath);

        foreach (var overwrite in propertyOverwrite) InternalProperties[overwrite.key] = overwrite.value;
    }
}