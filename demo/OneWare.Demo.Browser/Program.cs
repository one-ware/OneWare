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
        Trace.Listeners.Add(new ConsoleTraceListener());

        await BuildAvaloniaApp()
            .LogToTrace()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<WebDemoApp>();
}