namespace OneWare.Essentials.Debugging;

/// <summary>
/// What the user asked to debug, handed to <see cref="IDebuggerService.StartAsync"/>.
/// </summary>
/// <remarks>
/// A request with neither an <see cref="ExecutablePath"/> nor a <see cref="RemoteEndpoint"/> is
/// valid: the backend comes up without a target, which is what makes its command line usable for
/// checking the installation or attaching by hand.
/// </remarks>
/// <param name="AdapterId"><see cref="IDebugAdapter.Id"/> of the backend to use.</param>
/// <param name="ExecutablePath">Absolute path of the file carrying the program and its debug
/// symbols, usually an ELF. <c>null</c> for a target that already holds its program and has no
/// symbol file to offer — a softcore whose memory was filled during synthesis, for instance. The
/// session then works at instruction level: registers and stepping, but no source lines.</param>
/// <param name="RemoteEndpoint">Address of a remote stub, for example <c>localhost:3333</c>. When
/// set, the backend attaches to it instead of running the program on this machine. This is the
/// whole remote-debugging seam: a plugin that brings up a target passes the address it is
/// listening on and never learns which backend connects.</param>
/// <param name="WorkingDirectory">Directory the backend runs in. Defaults to the directory holding
/// <see cref="ExecutablePath"/>; set it explicitly when there is no executable, so that relative
/// paths typed into the backend's command line still resolve against the project.</param>
public sealed record DebugLaunchRequest(
    string AdapterId,
    string? ExecutablePath = null,
    string? RemoteEndpoint = null,
    string? WorkingDirectory = null);
