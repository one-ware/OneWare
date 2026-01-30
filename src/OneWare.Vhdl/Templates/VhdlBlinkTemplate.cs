using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Vhdl.Templates;

public class VhdlBlinkTemplate(ILogger logger, IMainDockService mainDockService) : IFpgaProjectTemplate
{
    public string Name => "VHDL Blink";

    public void FillTemplate(UniversalFpgaProjectRoot root)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Templates", "BlinkVhdl");

        try
        {
            var name = root.Header.Replace(" ", "");
            TemplateHelper.CopyDirectoryAndReplaceString(path, root.FullPath, ("%PROJECTNAME%", name));
            var file = root.AddFile(name + ".vhd");
            root.TopEntity = file;

            _ = mainDockService.OpenFileAsync(file);
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
        }
    }
}