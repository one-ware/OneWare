using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Platform.Storage;

namespace OneWare.Core.Data;

public static class Global
{
    public static string VersionCode => Assembly.GetEntryAssembly()!.GetName().Version?.ToString() ?? "";
}