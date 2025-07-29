using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Verilog.Templates;

public class VerilogBlinkTemplate(IDockService dockService) : IFpgaProjectTemplate
{
    public string Name => "Verilog Blink";

    public void FillTemplate(UniversalFpgaProjectRoot root)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Templates", "BlinkVerilog");

        try
        {
            var name = root.Header.Replace(" ", "");
            TemplateHelper.CopyDirectoryAndReplaceString(path, root.FullPath, ("%PROJECTNAME%", name));
            var file = root.AddFile(name + ".v");
            root.TopEntity = file;

            _ = dockService.OpenFileAsync(file);
        }
        catch (Exception e)
        {
            AppServices.Logger.LogError(e, e.Message);
        }
    }
}