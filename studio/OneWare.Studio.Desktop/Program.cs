using System;
using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Dialogs;
using Avalonia.Threading;
using Dock.Settings;
using Microsoft.Extensions.Logging;
using OneWare.Core.Data;
using OneWare.Core.Extensions;
using OneWare.Core.Services;
using OneWare.Core.Views.Windows;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Studio.Desktop.Cli;

namespace OneWare.Studio.Desktop;

internal abstract class Program
{
    private static readonly StudioInstanceChannel InstanceChannel = StudioInstanceChannel.Default;
    private static readonly Queue<string> PendingIpcMessages = new();
    private static FileStream? _lockFileStream;
    private static CancellationTokenSource? _ipcCancellation;
    private static bool _ipcReady;

    public static void ReleaseLock()
    {
        try
        {
            _ipcCancellation?.Cancel();
            _ipcCancellation?.Dispose();
            _lockFileStream?.Close();
            _lockFileStream?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error releasing lock: {ex.Message}");
        }
    }

    // This method is needed for IDE previewer infrastructure
    private static AppBuilder BuildAvaloniaApp()
    {
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        var maxGpuCacheBytes =
            StudioApp.SettingsService.GetSettingValue<string>("Experimental_MaxGpuResourceSizeBytes") switch
            {
                "128 MB" => 128L * 1024 * 1024,
                "256 MB" => 256L * 1024 * 1024,
                "512 MB" => 512L * 1024 * 1024,
                "1 GB" => 1024L * 1024 * 1024,
                _ => 1024L * 600 * 4 * 12 // Avalonia default ~28 MB
            };

        var x11RenderingModes = isLinux
            ? StudioApp.SettingsService.GetSettingValue<string>("Experimental_X11RenderingMode") switch
            {
                "EGL"      => new[] { X11RenderingMode.Egl, X11RenderingMode.Software },
                "Software" => new[] { X11RenderingMode.Software },
                "Vulkan"   => new[] { X11RenderingMode.Vulkan, X11RenderingMode.Software },
                _          => new[] { X11RenderingMode.Glx, X11RenderingMode.Software } // Default
            }
            : [X11RenderingMode.Glx, X11RenderingMode.Software];

        var app = AppBuilder.Configure<DesktopStudioApp>().UsePlatformDetect()
            .With(new SkiaOptions()
            {
                MaxGpuResourceSizeBytes = maxGpuCacheBytes
            })
            .With(new X11PlatformOptions
            {
                EnableMultiTouch = true,
                WmClass = "OneWare",
                RenderingMode = x11RenderingModes
            })
            .With(new Win32PlatformOptions
            {
                WinUICompositionBackdropCornerRadius = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Environment.OSVersion.Version.Build >= 22000 ? 8 : 0
                    : 0
            })
            .With(new MacOSPlatformOptions())
            .WithOneWareFonts()
            .LogToTrace();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && StudioApp.SettingsService.GetSettingValue<bool>("Experimental_UseManagedFileDialog"))
            app.UseManagedSystemDialogs();

        return app;
    }

    // On macOS this should not be necessary, but keeping for consistency
    private static bool TryBecomePrimaryInstance()
    {
        try
        {
            _lockFileStream = InstanceChannel.TryAcquirePrimaryLock();
            if (_lockFileStream == null)
            {
                Console.WriteLine("Another instance is already running.");
                return false;
            }

            Console.WriteLine($"Successfully acquired lock. PID: {Environment.ProcessId}");
            return true;
        }
        catch (Exception ex)
        {
            // Without a usable lock we cannot detect other instances, so start normally.
            Console.WriteLine($"Failed to create lock file: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// Called once the app can handle IPC messages. Messages received earlier are processed now.
    /// Must be called on the UI thread.
    /// </summary>
    public static void SignalIpcReady()
    {
        _ipcReady = true;
        while (PendingIpcMessages.TryDequeue(out var message))
            HandleIpcMessage(message);
    }

    private static void ReceiveIpcMessage(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_ipcReady)
                HandleIpcMessage(message);
            else
                PendingIpcMessages.Enqueue(message);
        });
    }

    private static void HandleIpcMessage(string message)
    {
        try
        {
            var logger = ContainerLocator.Container?.Resolve<ILogger>();
            logger?.Log($"Received IPC message: {message}");

            if (StudioInstanceChannel.TryParseCommand(message, out var command, out var argument))
            {
                _ = ContainerLocator.Container?.Resolve<IIpcCommandService>().ExecuteAsync(command, argument);
                return;
            }

            HandleOpenTarget(message);
        }
        catch (Exception ex)
        {
            var logger = ContainerLocator.Container?.Resolve<ILogger>();
            logger?.Error($"Error handling IPC message: {ex.Message}", ex);
        }
    }

    internal static void ActivateMainWindow()
    {
        var mainWindow = ContainerLocator.Container?.Resolve<MainWindow>();
        if (mainWindow == null)
            return;

        if (mainWindow.WindowState == WindowState.Minimized)
            mainWindow.WindowState = WindowState.Normal;

        mainWindow.Activate();
    }

    private static void HandleOpenTarget(string target)
    {
        var logger = ContainerLocator.Container?.Resolve<ILogger>();
        var applicationStateService = ContainerLocator.Container?.Resolve<IApplicationStateService>();

        ActivateMainWindow();

        // "activateWindow" is sent by older versions; it only brings the window to the front.
        if (target == "activateWindow")
            return;

        if (target.StartsWith("oneware://", StringComparison.OrdinalIgnoreCase))
        {
            logger?.Log($"Opening URL: {target}");
            applicationStateService?.ExecuteUrlLaunchActions(new Uri(target));
        }
        else if (File.Exists(target) || Directory.Exists(target))
        {
            var fullPath = Path.GetFullPath(target);
            logger?.Log($"Opening path: {fullPath}");
            applicationStateService?.ExecutePathLaunchActions(fullPath);
        }
        else
        {
            logger?.Warning($"Target not found or invalid: {target}");
        }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            StartupTimer.Mark("Main entered");

            if (StudioCli.IsCliInvocation(args))
                return StudioCli.Run(args);

            // oneware.exe is a console-subsystem copy for terminals; start the GUI detached from it.
            if (StudioCli.IsWindowsConsoleAlias)
                return StudioProcessController.StartStudio(args);

            var startupSymbols = OneWareStartupCommandLine.CreateSymbols();
            var rootCommand = OneWareStartupCommandLine.CreateRootCommand(startupSymbols);

            rootCommand.SetAction(parseResult =>
            {
                OneWareStartupCommandLine.ApplyEnvironmentVariables(parseResult, startupSymbols);
            });
            var commandLineParseResult = rootCommand.Parse(args);
            commandLineParseResult.Invoke();

            if (!TryBecomePrimaryInstance())
            {
                Console.WriteLine("Forwarding request...");

                var message = Environment.GetEnvironmentVariable("ONEWARE_OPEN_URL")
                              ?? Environment.GetEnvironmentVariable("ONEWARE_OPEN_PATH")
                              ?? StudioInstanceChannel.FormatCommand(StudioInstanceChannel.ActivateCommand);

                if (InstanceChannel.SendMessageAsync(message).GetAwaiter().GetResult())
                {
                    Console.WriteLine("Request forwarded successfully.");
                    return 0;
                }

                Console.Error.WriteLine("Failed to forward request to the running OneWare Studio instance.");
                return 1;
            }

            // We are the primary instance - start IPC server
            _ipcCancellation = new CancellationTokenSource();
            var ipcToken = _ipcCancellation.Token;
            _ = Task.Run(() => InstanceChannel.RunServerAsync(ReceiveIpcMessage,
                ex => Console.WriteLine($"IPC Server error: {ex.Message}"), ipcToken));

            var appBuilder = BuildAvaloniaApp();
            StartupTimer.Mark("Settings loaded, Avalonia setup started");

            var result = appBuilder.StartWithClassicDesktopLifetime(args);

            return result;
        }
        catch (Exception ex)
        {
            var crashReport =
                $"Version: {Global.VersionCode} OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}{Environment.NewLine}{ex}";

            if (ContainerLocator.Container?.IsRegistered<ILogger>() == true)
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