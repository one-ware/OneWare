using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IApplicationStateService
{
    public ApplicationProcess ActiveProcess { get; }

    public ApplicationProcess AddState(string status, AppState state, Action? terminate = null);

    public void RemoveState(ApplicationProcess key, string finishMessage = "Done");

    public Task TerminateActiveDialogAsync();

    public void RegisterAutoLaunchAction(Action<string?> action);
    
    public void RegisterPathLaunchAction(Action<string?> action);
    
    public void RegisterUrlLaunchAction(string key, Action<string?> action);
    
    public void RegisterShutdownAction(Action action);
    
    public void ExecuteAutoLaunchActions(string? value);
    
    public void ExecutePathLaunchActions(string? value);
    
    public void ExecuteUrlLaunchActions(Uri uri);

    public void ExecuteShutdownActions();

    public void TryShutdown();
    
    public void TryRestart();
}