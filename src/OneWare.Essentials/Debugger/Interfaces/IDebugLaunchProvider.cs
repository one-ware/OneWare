using OneWare.Essentials.Debugger.Entities;

namespace OneWare.Essentials.Debugger.Interfaces;

/// <summary>
/// Analogous to <see cref="DebugLaunchRequest"/>, but as the preparation step. The core asks
/// which provider fits the current project, has it prepare, and starts with whatever request
/// comes back. That keeps the entry point in the generic UI while everything target-specific
/// stays in the plugin.
/// </summary>
public interface IDebugLaunchProvider
{
    /// <summary>
    /// Shown in the launch selection of the debug panel.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Returns <see langword="true"/> if this provider can handle the active project.
    /// Must be cheap and free of side effects — the UI calls it to fill the selection.
    /// </summary>
    public bool CanPrepare();

    /// <summary>
    /// Brings the target up and returns the matching launch request.
    /// Returns <see langword="null"/> if preparation failed or was cancelled; the user has
    /// already been notified in that case.
    /// </summary>
    public Task<DebugLaunchRequest?> PrepareAsync(CancellationToken ct = default);

    /// <summary>
    /// Releases whatever <see cref="PrepareAsync"/> claimed. Also runs when the session ended
    /// on its own.
    /// </summary>
    public Task CleanupAsync();
}
