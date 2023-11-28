using System.Diagnostics;
using OneWare.SDK.Enums;
using OneWare.SDK.Models;

namespace OneWare.SDK.Services;

public interface IApplicationStateService
{
    public ApplicationProcess ActiveProcess { get; }
    
    public ApplicationProcess AddState(string status, AppState state, Action? terminate = null);

    public void RemoveState(ApplicationProcess key, string finishMessage = "Done");
    
    public Task TerminateActiveDialogAsync();
}