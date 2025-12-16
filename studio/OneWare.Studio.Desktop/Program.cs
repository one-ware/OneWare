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
            if (TryExtractUrl(ref args, out var detectedUrl))
            {
                Environment.SetEnvironmentVariable("ONEWARE_URL", detectedUrl);
            }
            
            Option<string> dirOption = new("--oneware-dir") 
                { Description = "Path to documents directory for OneWare Studio. (optional)" };
            Option<string> projectsDirOption = new("--oneware-projects-dir") 
                { Description = "Path to default projects directory for OneWare Studio. (optional)" };
            Option<string> appdataDirOption = new("--oneware-appdata-dir") 
                { Description = "Path to application data directory for OneWare Studio. (optional)" };
            Option<string> moduleOption = new("--modules") 
                { Description = "Adds plugin to OneWare Studio during initialization. (optional)" };
            Option<string> autoLaunchOption = new("--autolaunch") 
                { Description = "Auto launches a specific action after OneWare Studio is loaded. Can be used by plugins (optional)" };
            
            RootCommand rootCommand = new()
            {
                Options = { 
                    dirOption, 
                    appdataDirOption,
                    projectsDirOption,
                    moduleOption,
                    autoLaunchOption,
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
                
                var autoLaunchValue = parseResult.GetValue(autoLaunchOption);
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
    
    private static bool IsLaunchUrl(string s)
    {
        return Uri.TryCreate(s, UriKind.Absolute, out var uri)
               && !string.IsNullOrEmpty(uri.Scheme)
               // be strict: only accept your scheme(s)
               && uri.Scheme.Equals("oneware", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractUrl(ref string[] args, out string? url)
    {
        url = null;
        if (args.Length == 0) return false;

        // Prefer last argument (matches protocol launchers)
        if (IsLaunchUrl(args[^1]))
        {
            url = args[^1];
            args = args.Take(args.Length - 1).ToArray();
            return true;
        }
        
        for (int i = 0; i < args.Length; i++)
        {
            if (IsLaunchUrl(args[i]))
            {
                url = args[i];
                args = args.Where((_, idx) => idx != i).ToArray();
                return true;
            }
        }

        return false;
    }
}