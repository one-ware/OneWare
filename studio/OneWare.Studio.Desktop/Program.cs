using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Autofac;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Media;
using OneWare.Core.Data;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Studio.Desktop;

internal static class Program
{
    private static AppBuilder BuildAvaloniaApp()
    {
        var appBuilder = AppBuilder.Configure<DesktopStudioApp>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions
            {
                EnableMultiTouch = true,
                WmClass = "OneWare"
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

        // Try to use managed dialogs only if container has been built and setting is enabled
        try
        {
            var settingsService = DesktopStudioApp.Container?.Resolve<ISettingsService>();
            if (settingsService?.GetSettingValue<bool>("Experimental_UseManagedFileDialog") == true)
            {
                appBuilder.UseManagedSystemDialogs();
            }
        }
        catch
        {
            // Container might not be ready yet — skip managed dialogs
        }

        return appBuilder;
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
            var crashReport = $"Version: {Global.VersionCode} OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}{Environment.NewLine}{ex}";

            if (DesktopStudioApp.Container?.IsRegistered<ILogger>() == true)
            {
                DesktopStudioApp.Container.Resolve<ILogger>().Error(ex.Message, ex, false);
            }
            else
            {
                Console.WriteLine(crashReport);
            }

            try
            {
                var paths = DesktopStudioApp.Container?.Resolve<IPaths>();
                if (paths != null)
                {
                    var crashPath = Path.Combine(paths.CrashReportsDirectory,
                        "crash_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", DateTimeFormatInfo.InvariantInfo) + ".txt");

                    PlatformHelper.WriteTextFile(crashPath, crashReport);
                }
            }
            catch
            {
                // If paths can't be resolved, ignore crash reporting to disk
            }

#if DEBUG
            Console.ReadLine();
#endif
        }

        return 0;
    }
}
