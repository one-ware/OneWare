using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using OneWare.Core.ModuleLogic;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.Vcd.Viewer.ViewModels;

namespace OneWare.Studio.Browser;

public class WebStudioApp : StudioApp
{
    protected override string GetDefaultLayoutName => "Web";

    private static void CopyAvaloniaAssetIntoFolder(Uri asset, string location)
    {
        using var stream = AssetLoader.Open(asset);
        Directory.CreateDirectory(Path.GetDirectoryName(location)!);
        using var writer = File.OpenWrite(location);
        stream.CopyTo(writer);
    }

    protected override void ConfigureModuleCatalog(OneWareModuleCatalog moduleCatalog)
    {
        base.ConfigureModuleCatalog(moduleCatalog);
    }

    protected override async Task LoadContentAsync()
    {
        await base.LoadContentAsync();

        try
        {
            var testProj = Path.Combine(Services.Resolve<IPaths>().ProjectsDirectory, "DemoProject");

            Directory.CreateDirectory(testProj);

            //Highlighting will not work with NET9, wait for NET10
            CopyAvaloniaAssetIntoFolder(
                new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/DemoProject.fpgaproj"),
                Path.Combine(testProj, "DemoProject.fpgaproj"));
            //CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/CppTest.cpp"), Path.Combine(testProj, "CPP", "CppTest.cpp"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VhdlTest.vhd"),
                Path.Combine(testProj, "VHDL", "VhdlTest.vhd"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VerilogTest.v"),
                Path.Combine(testProj, "Verilog", "VerilogTest.v"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Studio.Browser/Assets/DemoFiles/VcdTest.vcd"),
                Path.Combine(testProj, "VCD", "VcdTest.vcd"));

            var dummy = await Services.Resolve<UniversalFpgaProjectManager>()
                .LoadProjectAsync(Path.Combine(testProj, "DemoProject.fpgaproj"));

            Services.Resolve<IProjectExplorerService>().Projects.Add(dummy);
            Services.Resolve<IProjectExplorerService>().ActiveProject = dummy;

            foreach (var file in dummy!.Files)
            {
                var vm = await Services.Resolve<IMainDockService>().OpenFileAsync(file);

                if (vm is VcdViewModel vcdViewModel)
                {
                    var signals = vcdViewModel.Scopes.SelectMany(x => x.Signals);
                    foreach (var s in signals) vcdViewModel.AddSignal(s);
                }
            }

            dummy.IsExpanded = true;
        }
        catch (Exception e)
        {
            Services.Resolve<ILogger>().Error(e.Message, e);
        }
    }
}