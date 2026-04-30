using OneWare.Essentials.EditorExtensions;

namespace OneWare.Debugger;

public interface IDebuggerService
{
    bool IsActive { get; }
    IDebugSession? CurrentSession { get; }
    DebugSessionState CurrentState { get; }
    BreakpointStore Breakpoints { get; }
    IReadOnlyList<IDebugAdapter> Adapters { get; }

    event EventHandler? StateChanged;

    Task<bool> StartAsync(DebugLaunchRequest launchRequest);
    void Stop();
    void Continue();
    void Pause();
    void StepInto();
    void StepOver();
    void StepOut();
    Task ExecuteRawCommandAsync(string command);
}
