using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

using OneWare.Core.Extensions;
using OneWare.Shared;
using OneWare.Shared.Extensions;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;

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
                var withProcess = _activeStates.Where(x => x.Process != null || x.Terminate != null).ToArray();
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
    public ApplicationProcess AddState(string status, AppState state, Process? process = null,
        Action? terminate = null)
    {
        lock (_activeLock)
        {
            var key = new ApplicationProcess
            {
                StatusMessage = status,
                State = state,
                Process = process,
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

    public async Task TerminateActiveAsync()
    {
        if(ActiveProcess.State == AppState.Idle) return;
        
        var result = await _windowService.ShowProceedWarningAsync("Are you sure you want to terminate the process?");

        if (result == MessageBoxStatus.Yes)
        {
            ActiveProcess.Terminated = true;
        
            if (ActiveProcess.Terminate != null) ActiveProcess.Terminate.Invoke();
            else if (ActiveProcess.Process is { } process && process.IsRunning()) process.Kill();
            RemoveState(ActiveProcess);
        }
    }
}