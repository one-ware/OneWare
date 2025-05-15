using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Autofac;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Media;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Demo.Desktop;

internal abstract class Program
{
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
            //.WithInterFont()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://OneWare.Core/Assets/Fonts#Noto Sans"
            })
            .LogToTrace();

        if (DemoApp.SettingsService?.GetSettingValue<bool>("Experimental_UseManagedFileDialog") == true)
            app.UseManagedSystemDialogs();

        return app;
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            var logger = DemoApp.Container?.ResolveOptional<ILogger>();

            if (logger is not null)
                logger.Error(ex.Message, ex, false);
            else
                Console.WriteLine(ex.ToString());

            PlatformHelper.WriteTextFile(
                Path.Combine(DemoApp.Paths.CrashReportsDirectory,
                    "crash_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", DateTimeFormatInfo.InvariantInfo) +
                    ".txt"), ex.ToString());

#if DEBUG
            Console.ReadLine();
#endif
        }

        return 0;
    }
}
