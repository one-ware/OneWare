using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Verilog.Templates;

public class VerilogBlinkSimulationTemplate(ILogger logger, IDockService dockService) : IFpgaProjectTemplate
{
    public string Name => "Verilog Blink with Simulation";

    public void FillTemplate(UniversalFpgaProjectRoot root)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Templates", "BlinkSimulationVerilog");

        try
        {
            var name = root.Header.Replace(" ", "");
            TemplateHelper.CopyDirectoryAndReplaceString(path, root.FullPath, ("%PROJECTNAME%", name));
            var file = root.AddFile(name + ".v");
            root.TopEntity = file;
            var file2 = root.AddFile(name + "_tb.v");
            root.RegisterTestBench(file2);

            _ = dockService.OpenFileAsync(file);
            _ = dockService.OpenFileAsync(file2);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
        }
    }
}