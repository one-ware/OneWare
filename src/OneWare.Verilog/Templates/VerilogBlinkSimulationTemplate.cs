using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Verilog.Templates;

public class VerilogBlinkSimulationTemplate(ILogger logger, IMainDockService mainDockService) : IFpgaProjectTemplate
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
            var file2 = root.AddFile(name + "_tb.v");
            
            _ = mainDockService.OpenFileAsync(file);
            _ = mainDockService.OpenFileAsync(file2);
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
        }
    }
}