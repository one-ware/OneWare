using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Logging;

[assembly: SupportedOSPlatform("browser")]

namespace OneWare.Demo.Browser;

internal partial class Program
{
    public static async Task Main(string[] args)
    {
#if  DEBUG
        Trace.Listeners.Add(new ConsoleTraceListener());
#endif

        await BuildAvaloniaApp()
            #if DEBUG
            .LogToTrace()
            #endif
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<WebDemoApp>();
}