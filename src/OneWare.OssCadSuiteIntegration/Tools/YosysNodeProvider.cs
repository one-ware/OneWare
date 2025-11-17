using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Tools;

public class YosysNodeProvider(IChildProcessService childProcess): INodeProvider
{
  
   public async Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file)
    {
        await childProcess.ExecuteShellAsync("openFPGALoader", [""],
            file.FullPath, "Running OpenFPGALoader...", AppState.Loading, true);
        throw new NotImplementedException();
    }

    public string GetDisplayName()
    {
        return "Yosys NodeProvider";
    }

    public string GetKey()
    {
        return "YosysNodeProvider";
    }
}