using System;
using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Dialogs;
using Avalonia.Media;
using Avalonia.Threading;
using Dock.Settings;
using Microsoft.Extensions.Logging;
using OneWare.CloudIntegration;
using OneWare.CloudIntegration.Services;
using OneWare.Core.Data;
using OneWare.Core.Services;
using OneWare.Core.Views.Windows;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Studio.Desktop;

internal abstract class Program
{
    private const string PipeName = "oneware-studio-ipc";
    private static FileStream? _lockFileStream;
    private static CancellationTokenSource? _ipcCancellation;
    private static string LockFilePath => Path.Combine(Path.GetTempPath(), "OneWare", "oneware-studio.lock");

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
            //.WithInterFont()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://OneWare.Core/Assets/Fonts#Noto Sans"
            })
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
            Directory.CreateDirectory(Path.GetDirectoryName(LockFilePath) ?? Path.GetTempPath());

            _lockFileStream = new FileStream(
                LockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                32,
                FileOptions.DeleteOnClose);

            var pidBytes = Encoding.UTF8.GetBytes(Environment.ProcessId.ToString());
            _lockFileStream.Write(pidBytes, 0, pidBytes.Length);
            _lockFileStream.Flush();

            Console.WriteLine($"Successfully acquired lock. PID: {Environment.ProcessId}");
            return true;
        }
        catch (IOException)
        {
            // File is locked by another process - check if that process is still running
            try
            {
                if (File.Exists(LockFilePath))
                {
                    var pidString = File.ReadAllText(LockFilePath).Trim();
                    if (int.TryParse(pidString, out var existingPid))
                        try
                        {
                            Process.GetProcessById(existingPid);
                            Console.WriteLine($"Another instance is running (PID: {existingPid})");
                            return false; // Process is still running
                        }
                        catch (ArgumentException)
                        {
                            // Process doesn't exist - stale lock file
                            Console.WriteLine("Stale lock file detected, cleaning up...");
                            File.Delete(LockFilePath);
                            return TryBecomePrimaryInstance(); // Retry
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking existing process: {ex.Message}");
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create lock file: {ex.Message}");
            return true;
        }
    }

    private static async Task<bool> TrySendToExistingInstanceAsync(string message)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            await client.ConnectAsync(2000);

            await using var writer = new StreamWriter(client, Encoding.UTF8);
            await writer.WriteAsync(message);
            await writer.FlushAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunIpcServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var message = await reader.ReadToEndAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(message))
                    Dispatcher.UIThread.Post(() => { HandleOpenTarget(message); });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"IPC Server error: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); // Wait before retry
                }
            }
    }

    private static void HandleOpenTarget(string target)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(target))
                return;

            var logger = ContainerLocator.Container?.Resolve<ILogger>();
            logger?.Log($"Received IPC message: {target}");

            if (target == "shutdown")
            {
                logger?.Log("Shutting down via IPC request");

                if (ContainerLocator.Container?.Resolve<IApplicationStateService>() is { } applicationStateService)
                    _ = applicationStateService.TryShutdownAsync();
                else if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
                    desktopApp.Shutdown();

                return;
            }

            if (target == OneWareCloudIntegrationModule.LogoutIpcMessage)
            {
                var settingsService = ContainerLocator.Container?.Resolve<ISettingsService>();
                var userId = settingsService?.GetSettingValue<string>(
                    OneWareCloudIntegrationModule.OneWareAccountUserIdKey);

                if (!string.IsNullOrWhiteSpace(userId))
                    ContainerLocator.Container?.Resolve<OneWareCloudLoginService>().Logout(userId);

                return;
            }

            var mainWindow = ContainerLocator.Container?.Resolve<MainWindow>();
            if (mainWindow != null)
            {
                if (mainWindow.WindowState == WindowState.Minimized)
                    mainWindow.WindowState = WindowState.Normal;

                mainWindow.Activate();
            }

            if (target == "activateWindow")
            {
                // Just activate the window
            }
            else if (target.StartsWith("oneware://", StringComparison.OrdinalIgnoreCase))
            {
                logger?.Log($"Opening URL: {target}");
                ContainerLocator.Container?.Resolve<IApplicationStateService>()
                    .ExecuteUrlLaunchActions(new Uri(target));
            }
            else if (File.Exists(target) || Directory.Exists(target))
            {
                var fullPath = Path.GetFullPath(target);
                logger?.Log($"Opening path: {fullPath}");

                ContainerLocator.Container?.Resolve<IApplicationStateService>().ExecutePathLaunchActions(fullPath);
            }
            else
            {
                logger?.Warning($"Target not found or invalid: {target}");
            }
        }
        catch (Exception ex)
        {
            var logger = ContainerLocator.Container?.Resolve<ILogger>();
            logger?.Error($"Error handling IPC message: {ex.Message}", ex);
        }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var startupSymbols = OneWareStartupCommandLine.CreateSymbols();
            var rootCommand = OneWareStartupCommandLine.CreateRootCommand(startupSymbols);

            rootCommand.SetAction(parseResult =>
            {
                OneWareStartupCommandLine.ApplyEnvironmentVariables(parseResult, startupSymbols);
            });
            var commandLineParseResult = rootCommand.Parse(args);
            commandLineParseResult.Invoke();

            if (args.LastOrDefault() is "--help" or "-h") return 0;

            // Check for single instance
            if (!TryBecomePrimaryInstance())
            {
                // Not the primary instance - try to forward the message
                Console.WriteLine("Another instance is already running. Forwarding request...");

                // Determine what to send to the existing instance
                string? messageToSend = null;

                if (Environment.GetEnvironmentVariable("ONEWARE_OPEN_URL") is { } url)
                    messageToSend = url;
                else if (Environment.GetEnvironmentVariable("ONEWARE_OPEN_PATH") is { } path) messageToSend = path;

                try
                {
                    var sendTask = TrySendToExistingInstanceAsync(messageToSend ?? "activateWindow");
                    var sendSuccess = sendTask.Wait(TimeSpan.FromSeconds(5));

                    if (sendSuccess)
                    {
                        Console.WriteLine("Request forwarded successfully.");
                        return 0;
                    }

                    Console.WriteLine("Failed to forward request to existing instance.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error forwarding request: {ex.Message}");
                }

                return 0;
            }

            // We are the primary instance - start IPC server
            _ipcCancellation = new CancellationTokenSource();

            _ = Task.Run(() => RunIpcServerAsync(_ipcCancellation.Token));

            var result = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

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