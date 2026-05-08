using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;
using OneWare.UniversalFpgaProjectSystem.Context;

namespace OneWare.OssCadSuiteIntegration.Tools;

public class GtkWaveService(IToolExecutionDispatcherService toolExecutionDispatcherService)
{
    private static readonly string[] GtkProperties = ["GtkwSaveFile", "GtkwWaveArgs"];
    public static readonly string[] GtkWaveformEndings = [".vcd", ".ghw", ".fst", ".lxt"];

    public async Task OpenInGtkWaveAsync(string filePath)
    {
        var context = await TestBenchContextManager.LoadContextAsync(filePath);
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        
        List<string> args = [Path.GetFileName(filePath)];

        foreach (var property in GtkProperties)
        {
            if (context.Properties.TryGetPropertyValue(property, out var jsonNode) && 
                jsonNode?.GetValueKind() == System.Text.Json.JsonValueKind.String)
            {
                var value = jsonNode.GetValue<string>();
                if (!string.IsNullOrEmpty(value))
                {
                    args.Add(value);
                }
            }
        }

        var toolCommand = ToolCommand.FromWeakParams("gtkwave", args, directory);
        toolExecutionDispatcherService.StartWeakProcess(toolCommand);
    }
}
