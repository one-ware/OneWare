using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Core.Data;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using System.CommandLine;
using System.Linq;
using OneWare.Core.Views.Windows;
using Microsoft.Extensions.Logging;
using OneWare.Studio.Desktop.Views;

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
            .With(new MacOSPlatformOptions()
            {
                
            })
            //.WithInterFont()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://OneWare.Core/Assets/Fonts#Noto Sans"
            })
            .LogToTrace();

        if (StudioApp.SettingsService.GetSettingValue<bool>("Experimental_UseManagedFileDialog") && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
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
                bufferSize: 32,
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
                    if (int.TryParse(pidString, out int existingPid))
                    {
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
        {
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
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        HandleOpenTarget(message);
                    });
                }
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
    }

    private static void HandleOpenTarget(string target)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(target))
                return;

            var logger = ContainerLocator.Container?.Resolve<ILogger>();
            logger?.Log($"Received IPC message: {target}");
            
            ContainerLocator.Container?.Resolve<MainWindow>()?.Activate();

            if (target == "activateWindow")
            {
                // Just activate the window
            }
            else if (target.StartsWith("oneware://", StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("ONEWARE_OPEN_URL", target);
                logger?.Log($"Opening URL: {target}");
                ContainerLocator.Container?.Resolve<IApplicationStateService>().ExecuteUrlLaunchActions(new Uri(target));
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
            Argument<string?> openArgument = new("open")
            {
                Description = "File/Folder path or oneware:// URI to open",
                DefaultValueFactory = (x) => null,
            };
            
            RootCommand rootCommand = new()
            {
                Options = { 
                    dirOption, 
                    appdataDirOption,
                    projectsDirOption,
                    moduleOption,
                    autoLaunchOption
                },
                Arguments =
                {
                    openArgument
                }
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
                
                var openValue = parseResult.GetValue(openArgument);
                if (!string.IsNullOrEmpty(openValue))
                {
                    if (openValue.StartsWith("oneware://", StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.SetEnvironmentVariable("ONEWARE_OPEN_URL", openValue);
                    }
                    else if (File.Exists(openValue) || Directory.Exists(openValue))
                    {
                        Environment.SetEnvironmentVariable("ONEWARE_OPEN_PATH", Path.GetFullPath(openValue));
                    }
                }
            });
            var commandLineParseResult = rootCommand.Parse(args);
            commandLineParseResult.Invoke();
            
            if(args.LastOrDefault() is "--help" or "-h")
            {
                return 0;
            }

            // Check for single instance
            if (!TryBecomePrimaryInstance())
            {
                // Not the primary instance - try to forward the message
                Console.WriteLine("Another instance is already running. Forwarding request...");
                
                // Determine what to send to the existing instance
                string? messageToSend = null;
                
                if (Environment.GetEnvironmentVariable("ONEWARE_OPEN_URL") is { } url)
                {
                    messageToSend = url;
                }
                else if (Environment.GetEnvironmentVariable("ONEWARE_OPEN_PATH") is { } path)
                {
                    messageToSend = path;
                }

                try
                {
                    var sendTask = TrySendToExistingInstanceAsync(messageToSend ?? "activateWindow");
                    sendTask.Wait(5000); // 5 second timeout
                        
                    if (sendTask.Result)
                    {
                        Console.WriteLine("Request forwarded successfully.");
                        return 0;
                    }
                    else
                    {
                        Console.WriteLine("Failed to forward request to existing instance.");
                    }
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
