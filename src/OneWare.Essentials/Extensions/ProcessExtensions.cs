using System.Diagnostics;

namespace OneWare.Essentials.Extensions;

public static class ProcessExtensions
{
    public static bool IsRunning(this Process? process)
    {
        if (process == null) return false;

        try
        {
            Process.GetProcessById(process.Id);
            if(process.HasExited) return false;
        }
        catch
        {
            return false;
        }

        return true;
    }
}