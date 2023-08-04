using System.Diagnostics;
using OneWare.Shared.Enums;
using OneWare.Shared.Models;

namespace OneWare.Shared.Services;

public interface IActive
{
    public ApplicationProcess AddState(string status, AppState state, Process? process = null,
        Action? terminate = null);

    public void RemoveState(ApplicationProcess key, string finishMessage = "Done");
    
    public ApplicationProcess ActiveProcess { get; }
}