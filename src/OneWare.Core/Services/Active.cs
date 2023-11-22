using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.SDK.Enums;
using OneWare.SDK.Extensions;
using OneWare.SDK.Models;
using OneWare.SDK.Services;

namespace OneWare.Core.Services;

public class Active : ObservableObject, IActive
{
    private readonly IWindowService _windowService;
    
    private readonly object _activeLock = new();

    private readonly ObservableCollection<ApplicationProcess> _activeStates = new();
    
    private ApplicationProcess _activeProcess = new ApplicationProcess(){State = AppState.Idle, StatusMessage = "Ready"};
    public ApplicationProcess ActiveProcess
    {
        get => _activeProcess;
        private set => SetProperty(ref _activeProcess, value);
    }
    
    public Active(IWindowService windowService)
    {
        _windowService = windowService;
        
        _activeStates.CollectionChanged += (_, i) =>
        {
            if (_activeStates.Count > 0)
            {
                //Check if active process is compiling
                var withProcess = _activeStates.Where(x => x.Terminate != null).ToArray();
                ActiveProcess = (withProcess.Any() ? withProcess.Last() : _activeStates.Last());
            }
            else
            {
                if (i.OldItems is {Count: > 0} && i.OldItems[0] is ApplicationProcess s)
                    ActiveProcess = (new ApplicationProcess(){State = AppState.Idle, StatusMessage = s.FinishMessage});
            }
        };
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
        if(ActiveProcess.State == AppState.Idle) return;
        
        var result = await _windowService.ShowProceedWarningAsync("Are you sure you want to terminate the process?");

        if (result == MessageBoxStatus.Yes)
        {
            ActiveProcess.Terminated = true;
            ActiveProcess.Terminate?.Invoke();
            RemoveState(ActiveProcess);
        }
    }
}