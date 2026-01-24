using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Vhdl.Templates;

public class VhdlBlinkSimulationTemplate(ILogger logger, IMainDockService mainDockService) : IFpgaProjectTemplate
{
    public string Name => "VHDL Blink with Simulation";

    public void FillTemplate(UniversalFpgaProjectRoot root)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Templates", "BlinkSimulationVhdl");

        try
        {
            var name = root.Header.Replace(" ", "");
            TemplateHelper.CopyDirectoryAndReplaceString(path, root.FullPath, ("%PROJECTNAME%", name));
            var file = root.AddFile(name + ".vhd");

            root.TopEntity = file;

            var file2 = root.AddFile(name + "_tb.vhd");

            root.RegisterTestBench(file2);

            _ = mainDockService.OpenFileAsync(file);
            _ = mainDockService.OpenFileAsync(file2);
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
        }
    }
}