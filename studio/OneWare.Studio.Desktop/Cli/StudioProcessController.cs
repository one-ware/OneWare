using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OneWare.Essentials.Helpers;

namespace OneWare.Studio.Desktop.Cli;

internal sealed class StudioProcessController(StudioInstanceChannel channel)
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

    private static readonly string StudioExecutableName =
        OperatingSystem.IsWindows() ? "OneWareStudio.exe" : "OneWareStudio";

    public StudioProcessController() : this(StudioInstanceChannel.Default)
    {
    }

    public static string StudioExecutablePath => Path.Combine(AppContext.BaseDirectory, StudioExecutableName);

    /// <summary>
    /// Starts the GUI executable as a separate process (used by the Windows console alias).
    /// Shell execute keeps the GUI from inheriting the terminal's handles.
    /// </summary>
    public static int StartStudio(IEnumerable<string> args)
    {
        var startInfo = new ProcessStartInfo(StudioExecutablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo);
        return process is null ? 1 : 0;
    }

    public async Task<int> StopStudio(CancellationToken cancellationToken)
    {
        if (!channel.IsStudioRunning())
        {
            Console.Error.WriteLine("ONE WARE Studio is not running.");
            return 0;
        }

        if (!await channel.SendCommandAsync(StudioInstanceChannel.ShutdownCommand, null, cancellationToken))
        {
            Console.Error.WriteLine("Could not reach ONE WARE Studio over IPC.");
            return 1;
        }

        if (await channel.WaitForExitAsync(StopTimeout, cancellationToken))
            return 0;

        Console.Error.WriteLine(
            "ONE WARE Studio is still running. If it asks about unsaved changes, confirm the dialog in Studio.");
        return 1;
    }
}
