namespace OneWare.Essentials.Debugger;

public interface IDebuggerService
{
    public IReadOnlyList<IDebugAdapter> Adapters { get; }
    
    // vorbereitungsschritt: wer zum aktiven Projekt passt, steht in der Startauswahl
    public IReadOnlyList<IDebugLaunchProvider> LaunchProviders { get; }
    
    public IDebugSession? CurrentSession { get; }
    
    public DebugSessionState State { get; }
    
    public bool IsActive { get; }
    
    public event EventHandler? StateChanged;
    
    public void RegisterAdapter<T>() where T : IDebugAdapter;
    
    // vorbereitungsschritt: Registrierung wie bei den Adaptern, aufgeloest ueber den Container
    public void RegisterLaunchProvider<T>() where T : IDebugLaunchProvider;
    
    public Task<bool> StartAsync(DebugLaunchRequest launchRequest);
    
    // vorbereitungsschritt: erst PrepareAsync, dann Start mit dessen Anforderung;
    // CleanupAsync laeuft, sobald die Sitzung endet - gleich auf welchem Weg
    public Task<bool> StartAsync(IDebugLaunchProvider provider, CancellationToken ct = default);
    
    public Task StopAsync();

    /// <inheritdoc cref="IDebugSession.ContinueAsync"/>
    public Task ContinueAsync();

    /// <inheritdoc cref="IDebugSession.PauseAsync"/>
    public Task PauseAsync();

    /// <inheritdoc cref="IDebugSession.StepIntoAsync"/>
    public Task StepIntoAsync();

    /// <inheritdoc cref="IDebugSession.StepOverAsync"/>
    public Task StepOverAsync();

    /// <inheritdoc cref="IDebugSession.StepOutAsync"/>
    public Task StepOutAsync();

    /// <inheritdoc cref="IDebugSession.ReadMemoryAsync"/>
    public Task<string?> ReadMemoryAsync(string address, int byteCount);

    /// <inheritdoc cref="IDebugSession.SendRawCommandAsync"/>
    public Task SendRawCommandAsync(string command);
}
