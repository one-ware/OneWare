using System.Text.RegularExpressions;

namespace OneWare.Essentials.Helpers;

public static class TimeHelper
{
    public static string FormatTime(long time, long timeScale, long range)
    {
        string[] units = ["fs", "ps", "ns", "µs", "ms", "s"];
        double[] scales = [1, 1e3, 1e6, 1e9, 1e12, 1e15];
        
        var unitIndex = 0;
        double value = time * timeScale;
        
        // Find the appropriate unit
        while (unitIndex < units.Length - 1 && value >= 1000)
        {
            value /= 1000;
            unitIndex++;
        }
        
        var precision = CalculatePrecision(range * timeScale, scales[unitIndex]);
        
        return $"{value.ToString($"F{precision}")} {units[unitIndex]}";
    }
    
    private static int CalculatePrecision(long rangeFs, double scale)
    {
        var normalizedRange = rangeFs / scale;

        return normalizedRange switch
        {
            < 0.5 => 4,
            < 1 => 3,
            < 10 => 2,
            < 100 => 1,
            _ => 0
        };
    }
    
    public static string GetTimeScaleFromUnit(long timescale)
    {
        return timescale switch
        {
            < 1000 => "fs",
            >= 1000 and < 1000_000 => "ps",
            >= 1000_000 and < 1000_000_000 => "ns",
            >= 1000_000_000 and < 1000_000_000_000 => "us",
            >= 1000_000_000_000 and < 1000_000_000_000_000 => "ms",
            >= 1000_000_000_000_000 => "s",
        };
    }
}