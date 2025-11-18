using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Media;
using OneWare.Core.Data;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using Prism.Ioc;
using System.CommandLine;
using System.Linq;

namespace OneWare.Studio.Desktop;

internal abstract class Program
{
    // This method is needed for IDE previewer infrastructure
    private static AppBuilder BuildAvaloniaApp()
    {
        var app = AppBuilder.Configure<DesktopStudioApp>().UsePlatformDetect()
            .With(new X11PlatformOptions
            {
                EnableMultiTouch = true,
                WmClass = "OneWare",
            })
            .With(new Win32PlatformOptions
            {
                WinUICompositionBackdropCornerRadius = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Environment.OSVersion.Version.Build >= 22000 ? 8 : 0 : 0
            })
            //.WithInterFont()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://OneWare.Core/Assets/Fonts#Noto Sans"
            })
            .LogToTrace();

        if (StudioApp.SettingsService.GetSettingValue<bool>("Experimental_UseManagedFileDialog"))
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
            Option<string> projectsDirOption = new("--oneware-projects-dir") 
                { Description = "Path to default projects directory for OneWare Studio. (optional)" };
            Option<string> appdataDirOption = new("--oneware-appdata-dir") 
                { Description = "Path to application data directory for OneWare Studio. (optional)" };
            Option<string> moduleOption = new("--modules") 
                { Description = "Adds plugin to OneWare Studio during initialization. (optional)" };
            Option<string> oneAiAutoLaunch = new("--autolaunch") 
                { Description = "Auto launches a specific action after OneWare Studio is loaded. Can be used by plugins (optional)" };

            RootCommand rootCommand = new()
            {
                Options = { 
                    dirOption, 
                    appdataDirOption,
                    projectsDirOption,
                    moduleOption
                },
            };
            
            rootCommand.SetAction((parseResult) =>
            {
                var dirValue = parseResult.GetValue(dirOption);
                if (!string.IsNullOrEmpty(dirValue))
                    Environment.SetEnvironmentVariable("ONEWARE_DIR", Path.GetFullPath(dirValue));

                var projectsDirValue = parseResult.GetValue(projectsDirOption);
                if (!string.IsNullOrEmpty(projectsDirValue))
                    Environment.SetEnvironmentVariable("ONEWARE_PROJECTS_DIR", Path.GetFullPath(projectsDirValue));
                
                var appdataDirValue = parseResult.GetValue(appdataDirOption);
                if (!string.IsNullOrEmpty(appdataDirValue))
                    Environment.SetEnvironmentVariable("ONEWARE_APPDATA_DIR", Path.GetFullPath(appdataDirValue));
                
                var moduleValue = parseResult.GetValue(moduleOption);
                if (!string.IsNullOrEmpty(moduleValue))
                    Environment.SetEnvironmentVariable("ONEWARE_MODULES", moduleValue);
                
                var autoLaunchValue = parseResult.GetValue(oneAiAutoLaunch);
                if (!string.IsNullOrEmpty(autoLaunchValue))
                    Environment.SetEnvironmentVariable("ONEWARE_AUTOLAUNCH", autoLaunchValue);
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
            var crashReport =
                $"Version: {Global.VersionCode} OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}{Environment.NewLine}{ex}";

            if (ContainerLocator.Container.IsRegistered<ILogger>())
                ContainerLocator.Container?.Resolve<ILogger>()?.Error(ex.Message, ex, false);
            else Console.WriteLine(crashReport);

            PlatformHelper.WriteTextFile(
                Path.Combine(StudioApp.Paths.CrashReportsDirectory,
                    "crash_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", DateTimeFormatInfo.InvariantInfo) +
                    ".txt"), crashReport);
#if DEBUG
            Console.ReadLine();
#endif
        }

        return 0;
    }
}