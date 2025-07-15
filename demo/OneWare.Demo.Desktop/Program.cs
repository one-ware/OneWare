using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Media;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using Prism.Ioc;
using System.CommandLine;
using System.Linq;

namespace OneWare.Demo.Desktop;

internal abstract class Program
{
    // This method is needed for IDE previewer infrastructure
    private static AppBuilder BuildAvaloniaApp()
    {
        var app = AppBuilder.Configure<DesktopDemoApp>().UsePlatformDetect()
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

        if (DemoApp.SettingsService.GetSettingValue<bool>("Experimental_UseManagedFileDialog"))
            app.UseManagedSystemDialogs();

        return app;
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Option<string> dirOption = new("--oneware-dir") 
                { Description = "Path to documents directory for OneWare Studio. (optional)" };
            Option<string> appdataDirOption = new("--oneware-appdata-dir") 
                { Description = "Path to application data directory for OneWare Studio. (optional)" };
            Option<string> moduleOption = new("--modules") 
                { Description = "Adds plugin to OneWare Studio during initialization. (optional)" };

            RootCommand rootCommand = new()
            {
                Options = { 
                    dirOption, 
                    appdataDirOption,
                    moduleOption
                },
            };
            
            rootCommand.SetAction((parseResult) =>
            {
                var dirValue = parseResult.GetValue(dirOption);
                if (!string.IsNullOrEmpty(dirValue))
                    Environment.SetEnvironmentVariable("ONEWARE_DIR", Path.GetFullPath(dirValue));

                var appdataDirValue = parseResult.GetValue(appdataDirOption);
                if (!string.IsNullOrEmpty(appdataDirValue))
                    Environment.SetEnvironmentVariable("ONEWARE_APPDATA_DIR", Path.GetFullPath(appdataDirValue));
                
                var moduleValue = parseResult.GetValue(moduleOption);
                if (!string.IsNullOrEmpty(moduleValue))
                    Environment.SetEnvironmentVariable("ONEWARE_MODULES", moduleValue);
            });
            var commandLineParseResult = rootCommand.Parse(args);
            commandLineParseResult.Invoke();
            
            if(args.LastOrDefault() is "--help" or "-h")
            {
                return 0;
            }
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            if (ContainerLocator.Container.IsRegistered<ILogger>())
                ContainerLocator.Container?.Resolve<ILogger>()?.Error(ex.Message, ex, false);
            else Console.WriteLine(ex.ToString());

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