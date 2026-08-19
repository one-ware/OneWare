using OneWare.Essentials.Debugger.Entities;

namespace OneWare.Essentials.Debugger.Interfaces;

/// <summary>
/// More of a session factory than a real adapter. The name is borrowed from VS Code's DAP
/// (Debug Adapter Protocol), where "debug adapter" is the term for the backend itself.
/// <see cref="CreateSession"/> is synchronous by intent, so that everything which can block or
/// fail happens in <see cref="IDebugSession.StartAsync"/> — one failure path instead of two.
/// </summary>
public interface IDebugAdapter
{
    /// <summary>
    /// Stable identifier, referenced by <see cref="DebugLaunchRequest.AdapterId"/>.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Shown when the user picks a backend.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Returns <see langword="true"/> if this adapter can handle the given request.
    /// Must be cheap and free of side effects — it decides whether to offer this adapter at all.
    /// </summary>
    public bool CanLaunch(DebugLaunchRequest launchRequest);

    /// <summary>
    /// Only constructs the session object; launching happens inside
    /// <see cref="IDebugSession.StartAsync"/>.
    /// </summary>
    public IDebugSession CreateSession(DebugLaunchRequest launchRequest);
}