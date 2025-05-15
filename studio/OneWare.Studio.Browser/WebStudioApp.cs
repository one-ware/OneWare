using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.Vcd.Viewer.ViewModels;

namespace OneWare.Studio.Browser;

public class WebStudioApp : StudioApp
{
    protected override string GetDefaultLayoutName => "Web";

    private readonly IPaths _paths;
    private readonly IProjectExplorerService _projectExplorer;
    private readonly IDockService _dockService;
    private readonly ILogger _logger;
    private readonly UniversalFpgaProjectManager _projectManager;

    public WebStudioApp(
        IPaths paths,
        IProjectExplorerService projectExplorer,
        IDockService dockService,
        ILogger logger,
        UniversalFpgaProjectManager projectManager)
    {
        _paths = paths;
        _projectExplorer = projectExplorer;
        _dockService = dockService;
        _logger = logger;
        _projectManager = projectManager;
    }

    private static void CopyAvaloniaAssetIntoFolder(Uri asset, string location)
    {
        using var stream = AssetLoader.Open(asset);
        var directory = Path.GetDirectoryName(location);
        if (directory != null)
            Directory.CreateDirectory(directory);

        using var writer = File.OpenWrite(location);
        stream.CopyTo(writer);
    }

    protected override async Task LoadContentAsync()
    {
        try
        {
            var testProj = Path.Combine(_paths.ProjectsDirectory, "DemoProject");
            Directory.CreateDirectory(testProj);

            // Copy demo project assets
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/DemoProject.fpgaproj"), Path.Combine(testProj, "DemoProject.fpgaproj"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VhdlTest.vhd"), Path.Combine(testProj, "VHDL", "VhdlTest.vhd"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VerilogTest.v"), Path.Combine(testProj, "Verilog", "VerilogTest.v"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VcdTest.vcd"), Path.Combine(testProj, "VCD", "VcdTest.vcd"));

            var projectPath = Path.Combine(testProj, "DemoProject.fpgaproj");
            var project = await _projectManager.LoadProjectAsync(projectPath);

            if (project is null)
            {
                _logger.Warning("Demo project could not be loaded.");
                return;
            }

            _projectExplorer.Projects.Add(project);
            _projectExplorer.ActiveProject = project;
            project.IsExpanded = true;

            foreach (var file in project.Files)
            {
                var vm = await _dockService.OpenFileAsync(file);

                if (vm is VcdViewModel vcdViewModel)
                {
                    var signals = vcdViewModel.Scopes.SelectMany(s => s.Signals);
                    foreach (var signal in signals)
                    {
                        vcdViewModel.AddSignal(signal);
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.Error("Failed to load demo project.", e);
        }
    }
}
