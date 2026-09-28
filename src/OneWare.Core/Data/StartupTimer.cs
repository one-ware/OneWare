using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.Core.Data;

/// <summary>
///     Collects coarse startup phase timings and logs them once the UI is visible.
/// </summary>
public static class StartupTimer
{
    private static readonly List<(string Phase, double TotalMs, double DeltaMs)> Marks = new();
    private static readonly Lock SyncRoot = new();
    private static double _lastMs;
    private static bool _reported;

    private static double ElapsedSinceProcessStartMs
    {
        get
        {
            try
            {
                return (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
            }
            catch
            {
                return Environment.TickCount64;
            }
        }
    }

    public static void Mark(string phase)
    {
        lock (SyncRoot)
        {
            if (_reported) return;
            var now = ElapsedSinceProcessStartMs;
            Marks.Add((phase, now, now - _lastMs));
            _lastMs = now;
        }
    }

    public static void Report(ILogger? logger)
    {
        string text;
        lock (SyncRoot)
        {
            if (_reported) return;
            _reported = true;

            var builder = new StringBuilder("Startup timings (ms since process start / delta):");
            foreach (var (phase, total, delta) in Marks)
                builder.Append($"{Environment.NewLine}  {total,7:F0} {delta,7:F0}  {phase}");
            text = builder.ToString();
        }

        if (logger != null) logger.Log(text);
        else Console.WriteLine(text);
    }
}
