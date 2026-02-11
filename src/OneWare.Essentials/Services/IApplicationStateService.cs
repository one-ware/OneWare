using System.Collections.ObjectModel;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IApplicationStateService
{
    /// <summary>
    /// True once shutdown has completed.
    /// </summary>
    public bool ShutdownComplete { get; }
    
    /// <summary>
    /// Current user-visible notifications.
    /// </summary>
    public ObservableCollection<ApplicationNotification> CurrentNotifications { get; }

    /// <summary>
    /// The active application process state.
    /// </summary>
    public ApplicationProcess ActiveProcess { get; }

    /// <summary>
    /// Adds a new application state/process.
    /// </summary>
    public ApplicationProcess AddState(string status, AppState state, Action? terminate = null);

    /// <summary>
    /// Removes an application state/process.
    /// </summary>
    public void RemoveState(ApplicationProcess key, string finishMessage = "Done");

    /// <summary>
    /// Attempts to close any active dialogs.
    /// </summary>
    public Task TerminateActiveDialogAsync();

    /// <summary>
    /// Registers an auto-launch action.
    /// </summary>
    public void RegisterAutoLaunchAction(Action<string?> action);

    /// <summary>
    /// Registers a path launch action.
    /// </summary>
    public void RegisterPathLaunchAction(Action<string?> action);

    /// <summary>
    /// Registers a URL launch action under a key.
    /// </summary>
    public void RegisterUrlLaunchAction(string key, Action<string?> action);

    /// <summary>
    /// Registers a shutdown action.
    /// </summary>
    public void RegisterShutdownAction(Action action);

    /// <summary>
    /// Registers a shutdown task that can veto shutdown.
    /// </summary>
    public void RegisterShutdownTask(Func<Task<bool>> task);

    /// <summary>
    /// Executes registered auto-launch actions.
    /// </summary>
    public void ExecuteAutoLaunchActions(string? value);

    /// <summary>
    /// Executes registered path launch actions.
    /// </summary>
    public void ExecutePathLaunchActions(string? value);

    /// <summary>
    /// Executes registered URL launch actions.
    /// </summary>
    public void ExecuteUrlLaunchActions(Uri uri);

    /// <summary>
    /// Attempts to shut down the application.
    /// </summary>
    public Task<bool> TryShutdownAsync();

    /// <summary>
    /// Attempts to restart the application.
    /// </summary>
    public Task<bool> TryRestartAsync();
    
    /// <summary>
    /// Adds a notification to the list.
    /// </summary>
    public void AddNotification(ApplicationNotification notification);
    
    /// <summary>
    /// Clears all notifications.
    /// </summary>
    public void ClearNotifications();
}
