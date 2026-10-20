namespace OneWare.SoftwareProjectSystem.Services;

public enum CompilerFamily
{
    Gcc,
    Clang
}

/// <summary>
/// A set of tools that builds software for one target: host programs or cross compiled firmware.
/// </summary>
/// <param name="Id">Stable id stored in the project (<c>kit</c>).</param>
/// <param name="Name">Display name.</param>
/// <param name="CCompiler">C compiler (path or name on PATH).</param>
/// <param name="CxxCompiler">C++ compiler (path or name on PATH).</param>
public sealed record ToolchainKit(string Id, string Name, string CCompiler, string CxxCompiler)
{
    public CompilerFamily Family { get; init; } = CompilerFamily.Gcc;

    /// <summary>
    /// True if the kit builds programs that run on this machine, so Run and local Debug are possible.
    /// </summary>
    public bool IsHost { get; init; } = true;

    /// <summary>
    /// GDB matching the target, if known. Null uses the debugger setting.
    /// </summary>
    public string? Gdb { get; init; }

    /// <summary>
    /// Target triple / prefix of cross toolchains, e.g. <c>arm-none-eabi</c>.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// File extension of produced programs: <c>.exe</c> for Windows host kits, <c>.elf</c> for cross kits.
    /// </summary>
    public string ExecutableExtension { get; init; } = string.Empty;
}
