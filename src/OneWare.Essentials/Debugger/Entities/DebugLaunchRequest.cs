namespace OneWare.Essentials.Debugger.Entities;

/// <summary>
/// What the user asked to debug. A request with neither an executable nor a remote endpoint is
/// valid — the backend then comes up without a target, which is what makes its command line
/// usable for checking the installation or attaching by hand.
/// <paramref name="RemoteEndpoint"/> is the whole remote seam: a plugin that brings up a target
/// passes the address it is listening on and never learns which backend connects.
/// </summary>
/// <param name="AdapterId">Identifies the backend, e.g. <c>GDB</c>.</param>
/// <param name="ExecutablePath">
/// Path to the executable, e.g. an ELF file. Carries the program and its debug symbols.
/// </param>
/// <param name="RemoteEndpoint">Remote stub address, e.g. <c>localhost:1234</c>.</param>
/// <param name="WorkingDirectory">Working directory for the debug session.</param>
public sealed record DebugLaunchRequest(
    string AdapterId,
    string? ExecutablePath = null,
    string? RemoteEndpoint = null,
    string? WorkingDirectory = null);
