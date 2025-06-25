using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Dialogs; // Required for .UseManagedSystemDialogs()
using Avalonia.Media;
using OneWare.Essentials.Helpers;
// Note: ISettingsService is NOT directly used here for configuration of AppBuilder options,
// as the DI container isn't fully built yet. IConfiguration is used instead.
// using OneWare.Essentials.Services; // Keep if other types from this namespace are used directly
using Autofac; // Still here for DemoApp.Container.Resolve<T> example (though moved to DemoApp)
using Microsoft.Extensions.Logging;
using Serilog; // For Log.Logger static property and Serilog configuration
using Microsoft.Extensions.Configuration; // Required for ConfigurationBuilder and IConfiguration
using Serilog.Extensions.Logging; // Required for AddSerilog
using OneWare.Demo; // Make sure to use the namespace where DemoApp resides

namespace OneWare.Demo.Desktop;

internal abstract class Program
{
    // A static ILogger for Program.cs itself, configured via Serilog/appsettings.json
    private static ILogger<Program>? _logger;

    // A static IConfiguration instance to load appsettings.json very early
    // for settings that need to be read before the full DI container is built.
    private static IConfiguration? _appConfiguration;

    // Static constructor: This runs once, before any static members are accessed
    // or any instances of Program are created (though Program is abstract and not instantiated).
    // It's the ideal place for very early, global setup like configuration and logging.
    static Program()
    {
        // 1. Build Configuration from appsettings.json
        // SetBasePath ensures it looks for appsettings.json in the application's executable directory.
        // AddJsonFile loads the primary configuration.
        // AddEnvironmentVariables allows environment variables to override settings (e.g., for CI/CD or deployment).
        _appConfiguration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)            
            .Build();

        // 2. Configure Serilog's static Log.Logger using the loaded configuration.
        // This makes Serilog ready to receive log events based on appsettings.json rules.
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(_appConfiguration) // This is key for appsettings.json integration
            .CreateLogger();

        // 3. Initialize the Microsoft.Extensions.Logging ILogger<Program> instance.
        // This logger will use the Serilog.Log.Logger as its underlying logging provider.
        _logger = new LoggerFactory().AddSerilog(Log.Logger).CreateLogger<Program>();
    }


    // This method is used by Avalonia's infrastructure to build the application.
    // It configures global Avalonia properties *before* the application's lifecycle begins.
    private static AppBuilder BuildAvaloniaApp()
    {
        var app = AppBuilder.Configure<DesktopDemoApp>() // Configures DesktopDemoApp as the main application class
            .UsePlatformDetect() // Auto-detects the platform (Windows, macOS, Linux)
            .With(new X11PlatformOptions // Linux-specific options
            {
                EnableMultiTouch = true
            })
            .With(new Win32PlatformOptions // Windows-specific options
            {
                // Sets corner radius for WinUI composition backdrop on Windows 11+
                WinUICompositionBackdropCornerRadius = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Environment.OSVersion.Version.Build >= 22000 ? 8 : 0
                    : 0
            })
            .With(new FontManagerOptions // Global font settings
            {
                DefaultFamilyName = "avares://OneWare.Core/Assets/Fonts#Noto Sans"
            })
            .LogToTrace(); // Enables Avalonia's internal logging to debug output

        // This section handles a specific Avalonia AppBuilder configuration
        // that needs to be set very early, before the full DI container (DemoApp.Container) is built.
        if (_appConfiguration != null)
        {
            // We directly read the setting from the IConfiguration instance.
            // This is necessary because ISettingsService is a DI-managed service
            // that is not available until DemoApp.OnFrameworkInitializationCompleted() runs.
            // The GetValue<T>(key, defaultValue) extension method is convenient for this.
            // "Experimental_UseManagedFileDialog" is assumed to be a top-level key or direct path.
            var useManagedDialogs = _appConfiguration.GetValue<bool>("Experimental_UseManagedFileDialog", RuntimeInformation.IsOSPlatform(OSPlatform.Linux));

            if (useManagedDialogs)
            {
                app.UseManagedSystemDialogs(); // Conditionally enables Avalonia's managed file dialogs
            }
            app.UseManagedSystemDialogs();
        }
        else
        {
            // This warning would indicate a critical failure in static constructor config loading.
            _logger?.LogWarning("Application configuration not loaded when attempting to configure file dialogs.");
        }

        return app;
    }

    // The main entry point of the application.
    [STAThread] // Marks the thread as a Single-Threaded Apartment, required for some UI operations.
    public static int Main(string[] args)
    {
        try
        {
            // Builds the Avalonia application and starts its classic desktop lifetime.
            // This is where Avalonia takes control, eventually calling DemoApp.OnFrameworkInitializationCompleted().
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Log any unhandled exceptions to the configured Serilog sink.
            // _logger is guaranteed to be initialized by the static constructor.
            _logger!.LogError(ex, "An unhandled exception occurred during application execution.");

            // Also write a crash report to a file for diagnostics.
            // DemoApp.Paths is a static field, so it's directly accessible here.
            PlatformHelper.WriteTextFile(
                Path.Combine(DemoApp.Paths.CrashReportsDirectory,
                    "crash_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", DateTimeFormatInfo.InvariantInfo) +
                    ".txt"), ex.ToString());
#if DEBUG
            Console.ReadLine(); // In debug builds, keep console open to see crash details.
#endif
        }

        return 0; // Return non-zero on error.
    }
}