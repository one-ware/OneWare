using OneWare.Essentials.Debugger.Entities;

namespace OneWare.Essentials.Debugger.Interfaces;

/// <summary>
/// Analogous to <see cref="DebugLaunchRequest"/>, but as the preparation step. The core asks
/// which preparer fits the current project, has it prepare, and starts with whatever request
/// comes back. That keeps the entry point in the generic UI while everything target-specific
/// stays in the plugin.
/// </summary>
public interface IDebugTargetPreparer
{
    /// <summary>
    /// Names the preparer in the status line and in the debug console.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Returns <see langword="true"/> if this preparer can handle the active project.
    /// Must be cheap and free of side effects — the UI calls it to pick a preparer.
    /// </summary>
    public bool CanPrepare();

    /// <summary>
    /// Brings the target up and returns the matching launch request. A running preparation
    /// cannot be aborted; the UI locks the start button and waits for it, so keep the steps
    /// short and report what is happening.
    /// Returns <see langword="null"/> if preparation failed; the user has already been notified
    /// in that case.
    /// </summary>
    public Task<DebugLaunchRequest?> PrepareAsync();

    /// <summary>
    /// Releases whatever <see cref="PrepareAsync"/> claimed. Also runs when the session ended
    /// on its own.
    /// </summary>
    public Task CleanupAsync();
}
