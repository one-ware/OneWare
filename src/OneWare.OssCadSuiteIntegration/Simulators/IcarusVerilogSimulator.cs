using System.Text.RegularExpressions;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.Tools;
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
    private readonly GtkWaveService _gtkWaveService;

    public IcarusVerilogSimulator(IChildProcessService childProcessService, IMainDockService mainDockService,
        IProjectExplorerService projectExplorerService, GtkWaveService gtkWaveService)
    {
        _childProcessService = childProcessService;
        _mainDockService = mainDockService;
        _projectExplorerService = projectExplorerService;
        _gtkWaveService = gtkWaveService;
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
        
        var activeTestBenchRelative = Path.GetRelativePath(root.FullPath, fullPath).ToUnixPath();

        var verilogFiles = root.GetFiles("*.v")
            .Where(x => !root.IsCompileExcluded(x))
            .Where(x => !root.IsTestBench(x) || x.EqualPaths(activeTestBenchRelative))
            .Select(x => x.ToUnixPath());

        _mainDockService.Show<IOutputService>();

        List<string> icarusVerilogArguments = [];
        icarusVerilogArguments.AddRange(["-o", vvpPath]);
        
        var settings = await TestBenchContextManager.LoadContextAsync(fullPath);
        var waveOutput = settings.GetBenchProperty(nameof(IcarusVerilogSimulatorToolbarViewModel.WaveOutputFormat)) ?? "VCD";

        var waveOutputArgument = waveOutput switch
        {
            "VCD" => "",
            "LXT2" => "-lxt2",
            "FST" => "-fst",
            _ => string.Empty
        };
        
        var additionalGhdlOptions =
            settings.GetBenchProperty(nameof(IcarusVerilogSimulatorToolbarViewModel.IcarusVerilogArguments));
        if (additionalGhdlOptions != null) icarusVerilogArguments.AddRange(additionalGhdlOptions.Split(' '));
        
        icarusVerilogArguments.AddRange(verilogFiles);
        
        var (result, _) = await _childProcessService.ExecuteShellAsync("iverilog", icarusVerilogArguments,
            root.FullPath, "Running IVerilog...", AppState.Loading, true);

        if (!result) return false;

        var (success2, output) = await _childProcessService.ExecuteShellAsync("vvp", [vvpPath, waveOutputArgument],
            root.FullPath, "Running VVP Simulation...", AppState.Loading, true);

        if (!success2) return false;
        
        var escapedEnding = waveOutput switch
        {
            "VCD" => ".vcd",
            "LXT2" => ".lxt",
            "FST" => ".fst",
            _ => string.Empty
        };
        
        var vcdFileRegex = new Regex($@".*info: dumpfile\s+(.+\{escapedEnding})\s+opened for output.");

        var match = vcdFileRegex.Match(output);
        if (!match.Success) return true;
        
        var fileRelativePath = match.Groups[1].Value;
        var fileFullPath = Path.Combine(root.FullPath, fileRelativePath);
        
        if (waveOutput == "VCD")
        {
            _ = await _mainDockService.OpenFileAsync(fileFullPath);
        }
        else
        {
            _ = _gtkWaveService.OpenInGtkWaveAsync(fileFullPath);
        }

        return true;
    }
}
