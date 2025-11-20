using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Text.Json;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
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