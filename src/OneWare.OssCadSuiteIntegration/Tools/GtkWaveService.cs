using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Context;

namespace OneWare.OssCadSuiteIntegration.Tools;

public class GtkWaveService(IChildProcessService childProcessService)
{
    private static readonly string[] GtkProperties = ["GtkwSaveFile", "GtkwWaveArgs"];

    public async Task OpenInGtkWaveAsync(IFile file)
    {
        var context = await TestBenchContextManager.LoadContextAsync(file);
        List<string> args = [Path.GetFileName(file.FullPath)];

        foreach (var property in GtkProperties)
            if (context.Properties.TryGetPropertyValue(property, out var jsonNode) && jsonNode != null && jsonNode.GetValueKind() == System.Text.Json.JsonValueKind.String)            
                args.Add(jsonNode.GetValue<string>());

        // save file has to be provided as second argument without "--save"
        childProcessService.StartWeakProcess("gtkwave", args, Path.GetDirectoryName(file.FullPath) ?? "");
    }
}