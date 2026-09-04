using OneWare.Essentials.Debugger.Entities;

namespace OneWare.Essentials.Debugger.Interfaces;

/// <summary>
/// Turns a <see cref="DebugLaunchRequest"/> into a session for one particular backend. Together
/// with <see cref="IDebugTargetPreparer"/> it forms the whole chain: the preparer brings the
/// target up and produces the request, the launcher decides who can serve it and builds the
/// session.
/// What VS Code's DAP calls a "debug adapter" is this — the name is deliberately not borrowed,
/// because this one is in-process and never speaks the protocol.
/// <see cref="CreateSession"/> is synchronous by intent, so that everything which can block or
/// fail happens in <see cref="IDebugSession.StartAsync"/> — one failure path instead of two.
/// </summary>
public interface IDebugSessionLauncher
{
    /// <summary>
    /// Stable identifier, referenced by <see cref="DebugLaunchRequest.BackendId"/>.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Returns <see langword="true"/> if this launcher can serve the given request.
    /// Must be cheap and free of side effects — it decides whether this launcher is used at all.
    /// </summary>
    public bool CanLaunch(DebugLaunchRequest launchRequest);

    /// <summary>
    /// Only constructs the session object; launching happens inside
    /// <see cref="IDebugSession.StartAsync"/>.
    /// </summary>
    public IDebugSession CreateSession(DebugLaunchRequest launchRequest);
}