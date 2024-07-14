using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;

namespace OneWare.OssCadSuiteIntegration.Tools;

public class GtkWaveService(IChildProcessService childProcessService)
{
    public void OpenInGtkWave(string path)
    {
        _ = childProcessService.ExecuteShellAsync("gtkwave", [Path.GetFileName(path)],
            Path.GetDirectoryName(path) ?? "", "Open in GtkWave...", AppState.Idle);
    }
}