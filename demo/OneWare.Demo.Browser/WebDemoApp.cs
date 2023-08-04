using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform;
using OneWare.FolderProjectSystem.Models;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Demo.Browser;

public class WebDemoApp : DemoApp
{
    protected override string GetDefaultLayoutName => "Web";

    private static void CopyAvaloniaAssetIntoFolder(Uri asset, string location)
    {
        using var stream = AssetLoader.Open(asset);
        using var writer = File.OpenWrite(location);
        stream.CopyTo(writer);
    }
    
    protected override async Task LoadContentAsync()
    {
        try
        {
            var testProj = Path.Combine(Container.Resolve<IPaths>().ProjectsDirectory, "Test");

            Directory.CreateDirectory(testProj);
            var dummy = new FolderProjectRoot(testProj);

            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Demo/Assets/DemoFiles/CppTest.cpp"), Path.Combine(testProj, "CppTest.cpp"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Demo/Assets/DemoFiles/VhdlTest.vhd"), Path.Combine(testProj, "VhdlTest.vhd"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Demo/Assets/DemoFiles/VerilogTest.v"), Path.Combine(testProj, "VerilogTest.v"));
            CopyAvaloniaAssetIntoFolder(new Uri("avares://OneWare.Demo/Assets/DemoFiles/VcdTest.vcd"), Path.Combine(testProj, "VcdTest.vcd"));
        
            var vhdl = dummy.AddFile("VhdlTest.vhd");
            var verilog = dummy.AddFile( "VerilogTest.v");
            var cpp = dummy.AddFile("CppTest.cpp");
            var vcd = dummy.AddFile("VcdTest.vcd");

            Container.Resolve<IProjectExplorerService>().Items.Add(dummy);
            Container.Resolve<IProjectExplorerService>().ActiveProject = dummy;
            dummy.IsExpanded = true;

            await Container.Resolve<IDockService>().OpenFileAsync(cpp!);
            await Container.Resolve<IDockService>().OpenFileAsync(vhdl!);
            await Container.Resolve<IDockService>().OpenFileAsync(verilog!);
            await Container.Resolve<IDockService>().OpenFileAsync(vcd!);
        }
        catch (Exception e)
        {
            Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }
}