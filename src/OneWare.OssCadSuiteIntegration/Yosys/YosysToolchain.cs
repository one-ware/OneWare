using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Yosys;

public class YosysToolchain : IFpgaToolchain
{
    public string Name => "Yosys";
    
    public void LoadConnections()
    {
        throw new NotImplementedException();
    }

    public void SaveConnections()
    {
        throw new NotImplementedException();
    }

    public void StartCompile()
    {
        throw new NotImplementedException();
    }
}