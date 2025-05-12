using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Avalonia.Platform;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.Vcd.Viewer.ViewModels;
using Prism.Ioc;

namespace OneWare.Studio.Browser;

public class WebStudioApp : StudioApp
{
    protected override string GetDefaultLayoutName => "Web";

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
            var paths = Container.Resolve<IPaths>();
            var projectExplorer = Container.Resolve<IProjectExplorerService>();
            var dockService = Container.Resolve<IDockService>();
            var logger = Container.Resolve<ILogger>();

            var testProj = Path.Combine(paths.ProjectsDirectory, "DemoProject");
            Directory.CreateDirectory(testProj);

            // Copy demo project assets
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/DemoProject.fpgaproj"), Path.Combine(testProj, "DemoProject.fpgaproj"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VhdlTest.vhd"), Path.Combine(testProj, "VHDL", "VhdlTest.vhd"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VerilogTest.v"), Path.Combine(testProj, "Verilog", "VerilogTest.v"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VcdTest.vcd"), Path.Combine(testProj, "VCD", "VcdTest.vcd"));

            var projectPath = Path.Combine(testProj, "DemoProject.fpgaproj");
            var projectManager = Container.Resolve<UniversalFpgaProjectManager>();
            var project = await projectManager.LoadProjectAsync(projectPath);

            if (project is null)
            {
                logger.Warning("Demo project could not be loaded.");
                return;
            }

            projectExplorer.Projects.Add(project);
            projectExplorer.ActiveProject = project;
            project.IsExpanded = true;

            foreach (var file in project.Files)
            {
                var vm = await dockService.OpenFileAsync(file);

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
            Container.Resolve<ILogger>().Error("Failed to load demo project.", e);
        }
    }
}
