using OneWare.Essentials.EditorExtensions;

namespace OneWare.Debugger;

public interface IDebugSession
{
    string AdapterId { get; }
    string DisplayName { get; }
    bool IsRunning { get; }
    bool SupportsRawCommand { get; }

    event EventHandler<DebugSessionStateChangedEventArgs>? StateChanged;
    event EventHandler<string>? OutputReceived;
    event EventHandler<string>? CommandRun;
    event EventHandler? Exited;

    Task<bool> StartAsync();
    void StartExecution();
    void Stop();
    void Continue();
    void Pause();
    void StepInto();
    void StepOver();
    void StepOut();
    Task ExecuteRawCommandAsync(string command);
    bool InsertBreakpoint(BreakPoint breakpoint);
    bool RemoveBreakpoint(BreakPoint breakpoint);
}
