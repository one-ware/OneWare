namespace OneWare.Essentials.Debugger;

// analog zu DebugLaunchRequest nur als vorbereitungschritt

public interface IDebugLaunchProvider
{
    public string DisplayName { get; }

    public bool CanPrepare();

    // bringt das Zielsystem hoch und liefert die dazu passende Anforderung;
    // null heisst gescheitert oder abgebrochen, der Nutzer ist dann bereits unterrichtet
    public Task<DebugLaunchRequest?> PrepareAsync(CancellationToken ct = default);

    // gibt frei, was PrepareAsync belegt hat - laeuft auch, wenn die Sitzung von selbst endete
    public Task CleanupAsync();
}
