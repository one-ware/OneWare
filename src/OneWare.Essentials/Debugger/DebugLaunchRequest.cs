namespace OneWare.Essentials.Debugger;

public sealed record DebugLaunchRequest(
    string AdapterId, // z.b. GDB a
    string? ExecutablePath = null, // z.b. ELF
    string? RemoteEndpoint = null, //z.b. localhost:1234
    string? WorkingDirectory = null); //z.b. .debug im projekt
