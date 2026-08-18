using OneWare.Essentials.Debugger.Entities;

namespace OneWare.Essentials.Debugger.Interfaces;

// Analogous to DebugLaunchRequest, but as the preparation step -> the core asks who fits the
// current project, has it prepare, and starts with whatever request comes back. That keeps the
// entry point in the generic UI while everything target specific stays in the plugin.
public interface IDebugLaunchProvider
{
    // Shown in the launch selection of the debug panel
    public string DisplayName { get; }

    // Must be cheap and free of side effects -> the UI calls it to fill the selection
    public bool CanPrepare();

    // Brings the target up and returns the matching request
    // -> null means failed or cancelled, and the user has already been told
    public Task<DebugLaunchRequest?> PrepareAsync(CancellationToken ct = default);

    // Releases whatever PrepareAsync claimed - also runs when the session ended on its own
    public Task CleanupAsync();
}
