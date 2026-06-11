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
        
        var builder = toolExecutionDispatcherService.CreateToolCommandBuilder("gtkwave")
            .WithWorkingDirectory(directory)
            .Add(Path.GetFileName(filePath));
        
        foreach (var property in GtkProperties)
        {
            if (context.Properties.TryGetPropertyValue(property, out var jsonNode) && 
                jsonNode?.GetValueKind() == System.Text.Json.JsonValueKind.String)
            {
                builder.AddIfNotNull(jsonNode.GetValue<string>());
            }
        }

        var toolCommand = builder.Build();
        toolExecutionDispatcherService.StartWeakProcess(toolCommand);
    }
}
