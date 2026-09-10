namespace OneWare.Essentials.Debugger.Entities;

/// <summary>
/// What the user asked to debug. A request with neither an executable nor a remote endpoint is
/// valid — the backend then comes up without a target, which is what makes its command line
/// usable for checking the installation or attaching by hand.
/// <paramref name="RemoteEndpoint"/> is the whole remote seam: a plugin that brings up a target
/// passes the address it is listening on and never learns which backend connects.
/// </summary>
/// <param name="BackendId">Identifies the backend, e.g. <c>GDB</c>.</param>
/// <param name="ExecutablePath">
/// Path to the executable, e.g. an ELF file. Carries the program and its debug symbols.
/// </param>
/// <param name="RemoteEndpoint">Remote stub address, e.g. <c>localhost:1234</c>.</param>
/// <param name="WorkingDirectory">Working directory for the debug session.</param>
/// <param name="InitCommands">
/// Commands the backend applies at startup, before it connects to the target, in this order.
/// Whoever brings a target up states here what the backend cannot learn from the executable
/// alone — a register description, for instance. Passing them with the request keeps the
/// handover in one place: the alternative is a file next to the executable whose name both
/// sides have to agree on without either contract saying so.
/// </param>
/// <param name="TargetProfile">
/// How the memory panel should read and present this target's memory, or
/// <see langword="null"/> for <see cref="DebugTargetProfile.Default"/> — a byte-addressed
/// machine. Whoever brings the target up is the only party that knows its memory geometry; the
/// panel would otherwise have to guess it or be told by the user.
/// </param>
public sealed record DebugLaunchRequest(
    string BackendId,
    string? ExecutablePath = null,
    string? RemoteEndpoint = null,
    string? WorkingDirectory = null,
    IReadOnlyList<string>? InitCommands = null,
    DebugTargetProfile? TargetProfile = null);
