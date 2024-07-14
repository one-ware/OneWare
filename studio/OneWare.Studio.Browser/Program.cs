using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;

[assembly: SupportedOSPlatform("browser")]

namespace OneWare.Studio.Browser;

internal class Program
{
    public static async Task Main(string[] args)
    {
#if DEBUG
        Trace.Listeners.Add(new ConsoleTraceListener());
#endif

        await BuildAvaloniaApp()
#if DEBUG
            .LogToTrace()
#endif
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<WebStudioApp>();
    }
}