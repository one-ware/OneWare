using OneWare.Essentials.Debugger.Entities;

namespace OneWare.Essentials.Debugger.Interfaces;

// More of a session factory than a real adapter -> the name is borrowed from VS Code's DAP
// (Debug Adapter Protocol), where "debug adapter" is the term for the backend itself.
// CreateSession is synchronous by intent, so that everything which can block or fail happens in
// IDebugSession.StartAsync -> one failure path instead of two. Launching happens in the session.
public interface IDebugAdapter
{
    // Stable identifier, referenced by DebugLaunchRequest.AdapterId
    public string Id { get; }

    // Shown when the user picks a backend
    public string DisplayName { get; }

    // Must be cheap and free of side effects -> it decides whether to offer this adapter at all
    public bool CanLaunch(DebugLaunchRequest launchRequest);

    // Only constructs the object, see the note above
    public IDebugSession CreateSession(DebugLaunchRequest launchRequest);
}
