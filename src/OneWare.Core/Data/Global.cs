using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Platform.Storage;

namespace OneWare.Core.Data;

public static class Global
{
    public static Version Version => Assembly.GetEntryAssembly()!.GetName().Version!;
    
    public static string VersionCode => Version.ToString() ?? "";
}