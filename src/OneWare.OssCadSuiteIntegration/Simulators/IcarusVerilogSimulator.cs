using System.Text.RegularExpressions;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.UniversalFpgaProjectSystem.Context;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Simulators;

public class IcarusVerilogSimulator : IFpgaSimulator
{
    private readonly IChildProcessService _childProcessService;
    private readonly IDockService _dockService;
    private readonly IProjectExplorerService _projectExplorerService;

    public string Name => "IVerilog";

    public UiExtension? TestBenchToolbarTopUiExtension { get; }

    public IcarusVerilogSimulator(IChildProcessService childProcessService, IDockService dockService,
        IProjectExplorerService projectExplorerService)
    {
        _childProcessService = childProcessService;
        _dockService = dockService;
        _projectExplorerService = projectExplorerService;
        TestBenchToolbarTopUiExtension = new UiExtension(x =>
        {
            if (x is TestBenchContext tb)
                return new IcarusVerilogSimulatorToolbarView()
                {
                    DataContext = new IcarusVerilogSimulatorToolbarViewModel(tb, this)
                };
            return null;
        });
    }

    public async Task<bool> SimulateAsync(IFile file)
    {
        if (file is IProjectFile projectFile)
        {
            var vvpPath = Path.Combine(projectFile.TopFolder!.RelativePath,
                Path.GetFileNameWithoutExtension(file.Name) + ".vvp").ToUnixPath();

            var verilogFiles = projectFile.Root.Files.Where(x => x.Extension == ".v")
                .Select(x => $"{x.RelativePath.ToUnixPath()}");

            _dockService.Show<IOutputService>();

            List<string> icarusVerilogArguments = ["-o", vvpPath];
            icarusVerilogArguments.AddRange(verilogFiles);

            var (result, _) = await _childProcessService.ExecuteShellAsync("iverilog", icarusVerilogArguments,
                projectFile.Root.FullPath, "Running IVerilog...", AppState.Loading, true);

            if (!result) return false;

            var (success2, output) = await _childProcessService.ExecuteShellAsync("vvp", [vvpPath],
                projectFile.Root.FullPath, "Running VVP Simulation...", AppState.Loading, true);

            if (!success2) return false;

            var vcdFileRegex = new Regex(@"VCD info: dumpfile\s+(.+\.vcd)\s+opened for output.");

            var match = vcdFileRegex.Match(output);

            if (match.Success)
            {
                var vcdFileRelativePath = match.Groups[1].Value;
                var vcdFileFullPath = Path.Combine(projectFile.Root!.FullPath, vcdFileRelativePath);

                await Task.Delay(50);

                var vcdFile = projectFile.Root.SearchRelativePath(vcdFileRelativePath.ToPlatformPath()) as IFile ??
                              _projectExplorerService.GetTemporaryFile(vcdFileFullPath);

                var doc = await _dockService.OpenFileAsync(vcdFile);
                if (doc is IStreamableDocument vcd)
                {
                    vcd.PrepareLiveStream();
                }
            }
            
            return true;
        }

        return false;
    }
}