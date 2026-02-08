using System.Text.RegularExpressions;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.UniversalFpgaProjectSystem.Context;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Simulators;

public class IcarusVerilogSimulator : IFpgaSimulator
{
    private readonly IChildProcessService _childProcessService;
    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;

    public IcarusVerilogSimulator(IChildProcessService childProcessService, IMainDockService mainDockService,
        IProjectExplorerService projectExplorerService)
    {
        _childProcessService = childProcessService;
        _mainDockService = mainDockService;
        _projectExplorerService = projectExplorerService;
        TestBenchToolbarTopUiExtension = new OneWareUiExtension(x =>
        {
            if (x is TestBenchContext tb)
                return new IcarusVerilogSimulatorToolbarView
                {
                    DataContext = new IcarusVerilogSimulatorToolbarViewModel(tb, this)
                };
            return null;
        });
    }

    public string Name => "IVerilog";

    public OneWareUiExtension? TestBenchToolbarTopUiExtension { get; }

    public async Task<bool> SimulateAsync(string fullPath)
    {
        if (_projectExplorerService.GetRootFromFile(fullPath) is not UniversalFpgaProjectRoot root) return false;
        var folderPath = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(folderPath)) return false;
        
        var relativeFolderPath = Path.GetRelativePath(root.FullPath, folderPath);
        
        var vvpPath = Path.Combine(relativeFolderPath, Path.GetFileNameWithoutExtension(fullPath) + ".vvp").ToUnixPath();

        var verilogFiles = root.GetFiles("*.v")
            .Where(x => !root.IsCompileExcluded(x))
            .Select(x => $"{x.ToUnixPath()}");

        _mainDockService.Show<IOutputService>();

        List<string> icarusVerilogArguments = ["-o", vvpPath];
        icarusVerilogArguments.AddRange(verilogFiles);

        var (result, _) = await _childProcessService.ExecuteShellAsync("iverilog", icarusVerilogArguments,
            root.FullPath, "Running IVerilog...", AppState.Loading, true);

        if (!result) return false;

        var (success2, output) = await _childProcessService.ExecuteShellAsync("vvp", [vvpPath],
            root.FullPath, "Running VVP Simulation...", AppState.Loading, true);

        if (!success2) return false;

        var vcdFileRegex = new Regex(@"VCD info: dumpfile\s+(.+\.vcd)\s+opened for output.");

        var match = vcdFileRegex.Match(output);

        if (match.Success)
        {
            var vcdFileRelativePath = match.Groups[1].Value;
            var vcdFileFullPath = Path.Combine(root.FullPath, vcdFileRelativePath);

            var doc = await _mainDockService.OpenFileAsync(vcdFileFullPath);
        }

        return true;
    }
}