using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class ApplicationStateService : ObservableObject, IApplicationStateService
{
    private readonly Lock _activeLock = new();

    private readonly ObservableCollection<ApplicationProcess> _activeStates = new();
    private readonly List<Action<string?>> _autoLaunchActions = new();
    private readonly ILogger _logger;

    private readonly List<Action<string?>> _pathLaunchActions = new();
    private readonly List<Action> _shutdownActions = new();
    private readonly List<Func<Task<bool>>> _shutdownTasks = new();
    private readonly Dictionary<string, Action<string?>> _urlLaunchActions = new();
    private readonly IWindowService _windowService;

    public ApplicationStateService(IWindowService windowService, ILogger logger)
    {
        _windowService = windowService;
        _logger = logger;

        _activeStates.CollectionChanged += (_, i) =>
        {
            if (_activeStates.Count > 0)
            {
                //Check if active process is compiling
                var withProcess = _activeStates.Where(x => x.Terminate != null).ToArray();
                ActiveProcess = withProcess.Any() ? withProcess.Last() : _activeStates.Last();
            }
            else
            {
                if (i.OldItems is { Count: > 0 } && i.OldItems[0] is ApplicationProcess s)
                    ActiveProcess = new ApplicationProcess { State = AppState.Idle, StatusMessage = s.FinishMessage };
            }
        };
    }

    public bool ShutdownComplete { get; private set; }
    
    public ObservableCollection<ApplicationNotification> CurrentNotifications { get; }

    public ApplicationProcess ActiveProcess
    {
        get;
        private set => SetProperty(ref field, value);
    } = new() { State = AppState.Idle, StatusMessage = "Ready" };

    /// <summary>
    ///     Use the key to remove the added state with RemoveState()
    /// </summary>
    public ApplicationProcess AddState(string status, AppState state,
        Action? terminate = null)
    {
        lock (_activeLock)
        {
            var key = new ApplicationProcess
            {
                StatusMessage = status,
                State = state,
                Terminate = terminate
            };
            _activeStates.Add(key);
            return key;
        }
    }

    public void RemoveState(ApplicationProcess key, string finishMessage = "Done")
    {
        lock (_activeLock)
        {
            key.FinishMessage = finishMessage;
            _activeStates.Remove(key);
        }
    }

    public async Task TerminateActiveDialogAsync()
    {
        if (ActiveProcess.State == AppState.Idle) return;

        var result = await _windowService.ShowProceedWarningAsync("Are you sure you want to terminate the process?");

        if (result == MessageBoxStatus.Yes)
        {
            ActiveProcess.Terminated = true;
            ActiveProcess.Terminate?.Invoke();
            RemoveState(ActiveProcess);
        }
    }

    public void RegisterAutoLaunchAction(Action<string?> action)
    {
        _autoLaunchActions.Add(action);
    }

    public void RegisterPathLaunchAction(Action<string?> action)
    {
        _pathLaunchActions.Add(action);
    }

    public void RegisterUrlLaunchAction(string key, Action<string?> action)
    {
        _urlLaunchActions.Add(key, action);
    }

    public void RegisterShutdownAction(Action action)
    {
        _shutdownActions.Add(action);
    }

    public void RegisterShutdownTask(Func<Task<bool>> task)
    {
        _shutdownTasks.Add(task);
    }

    public void ExecuteAutoLaunchActions(string? value)
    {
        foreach (var action in _autoLaunchActions) action.Invoke(value);
    }

    public void ExecutePathLaunchActions(string? value)
    {
        foreach (var action in _pathLaunchActions) action.Invoke(value);
    }

    public void ExecuteUrlLaunchActions(Uri uri)
    {
        if (_urlLaunchActions.TryGetValue(uri.Authority, out var action))
            action.Invoke(uri.LocalPath);
        else
            //Try to auto download extension if possible
            _ = AttemptAutoDownloadExtensionAsync(uri.Authority, uri.LocalPath);
    }

    public async Task<bool> TryRestartAsync()
    {
        var result = await TryShutdownAsync();
        if (result)
        {
            PerformRestart();
            return true;
        }

        return false;
    }

    public async Task<bool> TryShutdownAsync()
    {
        try
        {
            foreach (var shutdownTask in _shutdownTasks)
            {
                var result = await shutdownTask.Invoke();
                if (!result)
                {
                    _logger.Log("Shutdown aborted by shutdown task.");
                    return false;
                }
            }

            ShutdownComplete = true;

            Shutdown();

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex.Message, ex);
        }

        return false;
    }

    public void ExecuteShutdownActions()
    {
        foreach (var action in _shutdownActions) action.Invoke();
    }

    private void PerformRestart()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            
            // Remove last argument if it's not a flag
            if (args.Length > 0)
            {
                var lastArg = args.Last();

                if (!lastArg.StartsWith("-", StringComparison.Ordinal))
                {
                    args = args.Take(args.Length - 1).ToArray();
                }
            }
            
            if (string.IsNullOrEmpty(executablePath))
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Cannot restart: executable path not found");
                return;
            }

            // Platform-specific handling
            switch (PlatformHelper.Platform)
            {
                case PlatformId.WinX64:
                case PlatformId.WinArm64:
                {
                    // Windows: Use shell execute to start detached
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = string.Join(" ", args.Select(arg => $"\"{arg}\"")),
                        UseShellExecute = true,
                        WorkingDirectory = Environment.CurrentDirectory
                    };
                    Process.Start(startInfo);
                    break;
                }

                case PlatformId.LinuxX64:
                case PlatformId.LinuxArm64:
                {
                    PlatformHelper.ExecReplace(executablePath, args);
                    
                    /*
                    { // Regular Linux binary - use the executable path
                        var command = executablePath;
                        var commandArgs = string.Join(" ", args.Select(arg => $"\"{arg}\""));

                        // Use sh -c with nohup and & to properly detach the process
                        var fullCommand = $"nohup {command} {commandArgs} > /dev/null 2>&1 &";
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "/bin/sh",
                            Arguments = $"-c \"{fullCommand.Replace("\"", "\\\"")}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = Environment.CurrentDirectory
                        };
                        Process.Start(startInfo);
                    }
                    */
                    break;
                }
                case PlatformId.OsxX64:
                case PlatformId.OsxArm64:
                {
                    // macOS: Check if inside .app bundle
                    if (executablePath.Contains(".app/Contents/MacOS/"))
                    {
                        // Get the .app path - use 'open -n' which naturally detaches
                        var appPath = executablePath.Substring(0,
                            executablePath.IndexOf(".app/Contents/MacOS/", StringComparison.Ordinal) + 4);
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "open",
                            Arguments = $"-n \"{appPath}\"" + (args.Length > 0
                                ? " --args " + string.Join(" ", args.Select(arg => $"\"{arg}\""))
                                : ""),
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = Environment.CurrentDirectory
                        };
                        Process.Start(startInfo);
                    }
                    else
                    {
                        // Regular macOS binary - use nohup like Linux
                        var commandArgs = string.Join(" ", args.Select(arg => $"\"{arg}\""));
                        var fullCommand = $"nohup {executablePath} {commandArgs} > /dev/null 2>&1 &";
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "/bin/sh",
                            Arguments = $"-c \"{fullCommand.Replace("\"", "\\\"")}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = Environment.CurrentDirectory
                        };
                        Process.Start(startInfo);
                    }

                    break;
                }

                default:
                    ContainerLocator.Container.Resolve<ILogger>()
                        ?.Error($"Restart not supported on platform: {PlatformHelper.Platform}");
                    return;
            }
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error($"Failed to restart application: {ex.Message}", ex);
        }
    }

    private async Task AttemptAutoDownloadExtensionAsync(string autoLaunchId, string? value)
    {
        var state = AddState($"Running Autolaunch Action: {autoLaunchId}", AppState.Loading);

        try
        {
            var packageService = ContainerLocator.Container.Resolve<IPackageService>();
            await packageService.RefreshAsync();
            
            var package = packageService.Packages.Values
                .Where(x => x.Status != PackageStatus.Installed)
                .FirstOrDefault(x =>
                    x.Package.UrlLaunchIds != null &&
                    x.Package.UrlLaunchIds.Split(';').Select(y => y.Trim()).Contains(autoLaunchId));

            var packageWindowService = ContainerLocator.Container.Resolve<IPackageWindowService>();
            if (package is
                {
                    Package: { Id: not null }, Status: PackageStatus.UpdateAvailable or PackageStatus.Available
                })
            {
                var result = await packageWindowService.QuickInstallPackageAsync(package.Package.Id);
                if (result)
                {
                    if (_urlLaunchActions.TryGetValue(autoLaunchId, out var action))
                        action.Invoke(value);
                }
                else
                {
                    ContainerLocator.Container.Resolve<ILogger>()
                        .Warning($"Failed downloading package for ID: {autoLaunchId}");
                }
            }
            else
            {
                ContainerLocator.Container.Resolve<ILogger>()
                    .Warning($"No package found for auto launch id: {autoLaunchId}");
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }

        RemoveState(state);
    }

    private void Shutdown()
    {
        _logger.Log("Closed!");

        //Save settings
        ContainerLocator.Container.Resolve<ISettingsService>()
            .Save(ContainerLocator.Container.Resolve<IPaths>().SettingsPath);

        ExecuteShutdownActions();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
            desktopApp.Shutdown();
    }
    
    public void AddNotification(ApplicationNotification notification)
    {
        CurrentNotifications.Insert(0, notification);
    }

    public void ClearNotifications()
    {
        CurrentNotifications.Clear();
    }
}
