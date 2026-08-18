namespace OneWare.Essentials.Debugger.Entities;

// What the user asked to debug. A request with neither an executable nor an endpoint is valid ->
// the backend then comes up without a target, which is what makes its command line usable for
// checking the installation or attaching by hand.
// RemoteEndpoint is the whole remote seam: a plugin that brings up a target passes the address it
// is listening on and never learns which backend connects.
public sealed record DebugLaunchRequest(
    string AdapterId, // z.b. GDB
    string? ExecutablePath = null, // z.b. ELF, carries the program and its debug symbols
    string? RemoteEndpoint = null, //z.b. localhost:1234
    string? WorkingDirectory = null); //z.b. /.debug im projekt
