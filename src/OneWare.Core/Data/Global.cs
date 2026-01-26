using System.Reflection;

namespace OneWare.Core.Data;

public static class Global
{
    public static Version Version => Assembly.GetEntryAssembly()!.GetName().Version!;

    public static string VersionCode => Version.ToString() ?? "";
}