using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;

[assembly: SupportedOSPlatform("browser")]

namespace OneWare.Demo.Browser;

internal partial class Program
{
    private static async Task Main(string[] args) => await BuildAvaloniaApp()
        .With(new BrowserPlatformOptions
        {
            
        })
        .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<DemoApp>();
}