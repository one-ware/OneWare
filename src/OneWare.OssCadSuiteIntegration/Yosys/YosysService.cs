using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.OssCadSuiteIntegration.Yosys;

public class YosysService
{
    private readonly IChildProcessService _childProcessService;
    
    public YosysService(IChildProcessService childProcessService)
    {
        _childProcessService = childProcessService;
    }

    public async Task SynthAsync(UniversalFpgaProjectRoot project)
    {
        var fpga = project.Properties["Fpga"];
        var top = project.Properties["TopEntity"];

        var verilogFiles = string.Join(" ", project.Files.Where(x => x.Extension == ".v"));
        var yosysFlags = string.Empty;
        
        await _childProcessService.ExecuteShellAsync("yosys", 
            $"yosys -p \"synth_${fpga} -top ${top} -json ${project.FullPath}/synth.json\" ${yosysFlags} ${verilogFiles}",
            project.FullPath, "Yosys Synth");
    }
}