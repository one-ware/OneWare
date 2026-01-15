﻿using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Core.Views.Windows;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.Core.Services;

public class ApplicationStateService : ObservableObject, IApplicationStateService
{
    private readonly object _activeLock = new();

    private readonly ObservableCollection<ApplicationProcess> _activeStates = new();

    private readonly List<Action<string?>> _autoLaunchActions = new();
    private readonly Dictionary<string, Action<string?>> _urlLaunchActions = new();
    private readonly List<Action> _shutdownActions = new();
    private readonly IWindowService _windowService;

    private ApplicationProcess _activeProcess = new() { State = AppState.Idle, StatusMessage = "Ready" };

    public ApplicationStateService(IWindowService windowService)
    {
        _windowService = windowService;

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

    public ApplicationProcess ActiveProcess
    {
        get => _activeProcess;
        private set => SetProperty(ref _activeProcess, value);
    }

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
    
    public void RegisterUrlLaunchAction(string key, Action<string?> action)
    {
        _urlLaunchActions.Add(key, action);
    }

    public void RegisterShutdownAction(Action action)
    {
        _shutdownActions.Add(action);
    }

    public void ExecuteAutoLaunchActions(string? value)
    {
        foreach (var action in _autoLaunchActions) action.Invoke(value);
    }
    
    public void ExecuteUrlLaunchActions(string key, string? value)
    {
        if(_urlLaunchActions.TryGetValue(key, out var action))
            action.Invoke(value);
    }

    public void ExecuteShutdownActions()
    {
        foreach (var action in _shutdownActions) action.Invoke();
    }

    public void TryShutdown()
    {
        Dispatcher.UIThread.Post(() => ContainerLocator.Container.Resolve<MainWindow>().Close());
    }
    
    public void TryRestart()
    {
        RegisterShutdownAction(PerformRestart);
        TryShutdown();
    }
    
    private void PerformRestart()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            
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
                    string command;
                    string commandArgs;
                    
                    // Linux: Check if running in Flatpak or Snap
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID")))
                    {
                        // Running in Flatpak
                        var flatpakId = Environment.GetEnvironmentVariable("FLATPAK_ID");
                        command = "flatpak";
                        commandArgs = $"run {flatpakId}";
                        if (args.Length > 0)
                            commandArgs += " " + string.Join(" ", args.Select(arg => $"\"{arg}\""));
                    }
                    else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNAP")))
                    {
                        // Running in Snap
                        var snapName = Environment.GetEnvironmentVariable("SNAP_NAME");
                        command = "snap";
                        commandArgs = $"run {snapName}";
                        if (args.Length > 0)
                            commandArgs += " " + string.Join(" ", args.Select(arg => $"\"{arg}\""));
                    }
                    else
                    {
                        // Regular Linux binary - use the executable path
                        command = executablePath;
                        commandArgs = string.Join(" ", args.Select(arg => $"\"{arg}\""));
                    }

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
                    break;
                }

                case PlatformId.OsxX64:
                case PlatformId.OsxArm64:
                {
                    // macOS: Check if inside .app bundle
                    if (executablePath.Contains(".app/Contents/MacOS/"))
                    {
                        // Get the .app path - use 'open -n' which naturally detaches
                        var appPath = executablePath.Substring(0, executablePath.IndexOf(".app/Contents/MacOS/", StringComparison.Ordinal) + 4);
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "open",
                            Arguments = $"-n \"{appPath}\"" + (args.Length > 0 ? " --args " + string.Join(" ", args.Select(arg => $"\"{arg}\"")) : ""),
                            UseShellExecute = true,
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
                    ContainerLocator.Container.Resolve<ILogger>()?.Error($"Restart not supported on platform: {PlatformHelper.Platform}");
                    return;
            }
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error($"Failed to restart application: {ex.Message}", ex);
        }
    }
}