using OneWare.Essentials.Models;
using OneWare.OssCadSuiteIntegration.Yosys;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Tools;

public class YosysNodeProvider(YosysService service) : INodeProvider
{
    public string Name => "Verilog_Yosys";

    public string[] SupportedLanguages => ["Verilog"];

    public async Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file)
    {
        return await service.ExtractNodesAsync(file);
    }
}