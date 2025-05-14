using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Core.Views.Windows;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using System.Linq;
using System.Threading.Tasks;

namespace OneWare.Core.Services;

public class ApplicationStateService : ObservableObject, IApplicationStateService
{
    private readonly object _activeLock = new();
    private readonly ObservableCollection<ApplicationProcess> _activeStates = new();
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
                // Check if active process is compiling
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
    public ApplicationProcess AddState(string status, AppState state, Action? terminate = null)
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

    public void RegisterShutdownAction(Action action)
    {
        _shutdownActions.Add(action);
    }

    public void ExecuteShutdownActions()
    {
        foreach (var action in _shutdownActions) action.Invoke();
    }

    public void TryShutdown()
    {
        Dispatcher.UIThread.Post(() => _windowService.CloseMainWindow());
    }
}
