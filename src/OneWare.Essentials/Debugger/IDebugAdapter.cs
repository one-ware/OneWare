namespace OneWare.Essentials.Debugger;

/// <summary>
/// A debug backend: probes whether it can serve a request and, if so, produces a session.
/// </summary>
/// <remarks>
/// The core ships one adapter for GDB, which also covers remote targets through
/// <see cref="DebugLaunchRequest.RemoteEndpoint"/>. A plugin only needs its own adapter when GDB
/// cannot speak to its target at all.
/// <para>
/// Register with <see cref="IDebuggerService.RegisterAdapter{T}"/>.
/// </para>
/// </remarks>
public interface IDebugAdapter
{
    /// <summary>
    /// Stable identifier, referenced by <see cref="DebugLaunchRequest.AdapterId"/>.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Display name shown when the user picks a backend.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Whether this adapter can serve the request. Must be cheap and free of side effects — it is
    /// called to decide whether to offer the adapter at all.
    /// </summary>
    public bool CanLaunch(DebugLaunchRequest launchRequest);

    /// <summary>
    /// Builds the session. Synchronous on purpose: this only constructs the object, and everything
    /// that can block or fail happens in <see cref="IDebugSession.StartAsync"/>. That keeps the one
    /// failure path in one place instead of splitting it across construction and startup.
    /// </summary>
    public IDebugSession CreateSession(DebugLaunchRequest launchRequest);
}
