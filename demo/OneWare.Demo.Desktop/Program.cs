using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using Serilog;

namespace OneWare.Demo.Desktop
{
    internal class Program
    {
        private static readonly ILogger<PlatformHelper> Logger = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog();
        }).CreateLogger<PlatformHelper>();

        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                // Log the error to the console
                Console.WriteLine(ex.ToString());

                // Write crash log to file
                var platformHelper = new PlatformHelper(Logger);
                string crashReportPath = Path.Combine(
                    DemoApp.Paths.CrashReportsDirectory,
                    $"crash_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", DateTimeFormatInfo.InvariantInfo)}.txt");

                // Ensure the directory exists
                Directory.CreateDirectory(DemoApp.Paths.CrashReportsDirectory);

                platformHelper.WriteTextFile(crashReportPath, ex.ToString());

#if DEBUG
                Console.ReadLine();
#endif
            }

            return 0;
        }

        // This method is needed for IDE previewer infrastructure
        private static AppBuilder BuildAvaloniaApp()
        {
            var app = AppBuilder.Configure<DesktopDemoApp>()
                .UsePlatformDetect()
                .With(new X11PlatformOptions
                {
                    EnableMultiTouch = true
                })
                .With(new Win32PlatformOptions
                {
                    WinUICompositionBackdropCornerRadius = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? Environment.OSVersion.Version.Build >= 22000 ? 8 : 0
                        : 0
                })
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "avares://OneWare.Core/Assets/Fonts#Noto Sans"
                })
                .LogToTrace();

            if (DemoApp.SettingsService.GetSettingValue<bool>("Experimental_UseManagedFileDialog"))
            {
                app.UseManagedSystemDialogs();
            }

            return app;
        }
    }
}
