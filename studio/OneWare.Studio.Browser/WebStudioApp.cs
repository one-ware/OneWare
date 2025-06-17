using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using OneWare.Core.Services;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.Vcd.Viewer.ViewModels;

namespace OneWare.Studio.Browser;

public class WebStudioApp : StudioApp
{
    private readonly IPaths _paths;
    private readonly UniversalFpgaProjectManager _universalFpgaProjectManager;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ILogger<WebStudioApp> _logger;
    private readonly IDockService _dockService;

    public WebStudioApp(IPaths paths,
                        IProjectExplorerService projectExplorerService,
                        ILogger<WebStudioApp> logger,
                        IDockService dockService,
                        UniversalFpgaProjectManager universalFpgaProjectManager)
    {
        _paths = paths;
        _universalFpgaProjectManager = universalFpgaProjectManager;
        _projectExplorerService = projectExplorerService;
        _logger = logger;
        _dockService = dockService;
    }

    protected override string GetDefaultLayoutName => "Web";

    private static void CopyAvaloniaAssetIntoFolder(Uri asset, string location)
    {
        using var stream = AssetLoader.Open(asset);
        Directory.CreateDirectory(Path.GetDirectoryName(location)!);
        using var writer = File.OpenWrite(location);
        stream.CopyTo(writer);
    }

    protected override async Task LoadContentAsync()
    {
        try
        {
            var testProj = Path.Combine(_paths.ProjectsDirectory, "DemoProject");

            Directory.CreateDirectory(testProj);

            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/DemoProject.fpgaproj"), Path.Combine(testProj, "DemoProject.fpgaproj"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VhdlTest.vhd"), Path.Combine(testProj, "VHDL", "VhdlTest.vhd"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VerilogTest.v"), Path.Combine(testProj, "Verilog", "VerilogTest.v"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VcdTest.vcd"), Path.Combine(testProj, "VCD", "VcdTest.vcd"));

            var dummy = await _universalFpgaProjectManager.LoadProjectAsync(Path.Combine(testProj, "DemoProject.fpgaproj"));

            _projectExplorerService.Projects.Add(dummy);
            _projectExplorerService.ActiveProject = dummy;

            foreach (var file in dummy!.Files)
            {
                var vm = await _dockService.OpenFileAsync(file);

                if (vm is VcdViewModel vcdViewModel)
                {
                    var signals = vcdViewModel.Scopes.SelectMany(x => x.Signals);
                    foreach (var s in signals)
                    {
                        vcdViewModel.AddSignal(s);
                    }
                }
            }
            dummy.IsExpanded = true;
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message, e);
        }
    }
}
