using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;
using OneWare.OssCadSuiteIntegration.Tools;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.UniversalFpgaProjectSystem.Context;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Simulators;

public class IcarusVerilogSimulator : IFpgaSimulator
{
    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly GtkWaveService _gtkWaveService;
    private readonly IToolExecutionDispatcherService  _toolExecutionDispatcherService;
    private readonly ILogger _logger;

    public IcarusVerilogSimulator(ILogger logger, IMainDockService mainDockService,
        IProjectExplorerService projectExplorerService, GtkWaveService gtkWaveService,
        IToolExecutionDispatcherService toolExecutionDispatcherService)
    {
        _logger = logger;
        _toolExecutionDispatcherService =  toolExecutionDispatcherService;
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
        
        var settings = await TestBenchContextManager.LoadContextAsync(fullPath);
        var waveOutput = settings.GetBenchProperty(nameof(IcarusVerilogSimulatorToolbarViewModel.WaveOutputFormat)) ?? "VCD";
        
        var command = _toolExecutionDispatcherService.CreateToolCommandBuilder("iverilog")
            .WithWorkingDirectory(root.FullPath)
            .WithStatus("Running IVerilog..", AppState.Loading)
            .WithTimer(true)
            .AddPathOption("-o", vvpPath)
            .AddRawArguments(settings.GetBenchProperty(nameof(IcarusVerilogSimulatorToolbarViewModel.IcarusVerilogArguments)))
            .AddPaths(verilogFiles)
            .Build();
        
        var (resultIVerilog, _) = await _toolExecutionDispatcherService.ExecuteAsync(command);
        
        if (!resultIVerilog)
        {
            _logger.LogWarning("IVerilog failed");
            return false;
        }
        

        var vvpCommand = _toolExecutionDispatcherService.CreateToolCommandBuilder("vvp").WithWorkingDirectory(root.FullPath)
            .WithStatus("Running VPP Simulation", AppState.Loading)
            .WithTimer(true)
            .Add(vvpPath)
            .AddIf(waveOutput == "LXT2", "-lxt2")
            .AddIf(waveOutput == "FST", "-fst").Build();
        
        var (resultVvp, outputVvp) = await _toolExecutionDispatcherService.ExecuteAsync(vvpCommand);
        
        if (!resultVvp)
        {
            _logger.LogWarning("VVP failed");
            return false;
        }
        
        var escapedEnding = waveOutput switch
        {
            "VCD" => ".vcd",
            "LXT2" => ".lxt",
            "FST" => ".fst",
            _ => string.Empty
        };
        
        var vcdFileRegex = new Regex($@".*info: dumpfile\s+(.+\{escapedEnding})\s+opened for output.");

        var match = vcdFileRegex.Match(outputVvp);
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
