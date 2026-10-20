using OneWare.SoftwareProjectSystem.Services;

namespace OneWare.SoftwareProjectSystem.Build;

/// <summary>
/// Everything a build system needs, independent of the project model so it can be tested without the UI.
/// </summary>
public sealed record SoftwareBuildRequest
{
    public required string RootDirectory { get; init; }

    /// <summary>
    /// Build output of the selected configuration, e.g. <c>build/Debug</c>.
    /// </summary>
    public required string BuildDirectory { get; init; }

    /// <summary>
    /// Where <c>compile_commands.json</c> is placed for clangd (the folder clangd searches: <c>build</c>).
    /// </summary>
    public required string CompileCommandsDirectory { get; init; }

    public required ToolchainKit Kit { get; init; }
    public required string Configuration { get; init; }
    public required string OutputName { get; init; }

    /// <summary>
    /// Source files relative to <see cref="RootDirectory" /> (only used by the simple build).
    /// </summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    public IReadOnlyList<string> IncludeDirectories { get; init; } = [];
    public IReadOnlyList<string> Defines { get; init; } = [];
    public IReadOnlyList<string> CompilerFlags { get; init; } = [];
    public IReadOnlyList<string> LinkerFlags { get; init; } = [];
    public IReadOnlyList<string> Libraries { get; init; } = [];
    public IReadOnlyList<string> CMakeArguments { get; init; } = [];
    public string? CStandard { get; init; }
    public string? CxxStandard { get; init; }

    /// <summary>
    /// True if <see cref="OutputName" /> was set explicitly (CMake then prefers that target).
    /// </summary>
    public bool HasExplicitOutputName { get; init; }

    /// <summary>
    /// Receives every output line of the invoked tools.
    /// </summary>
    public required Action<string> Output { get; init; }

    /// <summary>
    /// Receives the command lines that are executed.
    /// </summary>
    public Action<string>? Command { get; init; }

    public bool IsRelease => string.Equals(Configuration, "Release", StringComparison.OrdinalIgnoreCase);
}

public sealed record SoftwareBuildResult(bool Success, string? Executable, string? Message = null)
{
    public static SoftwareBuildResult Failed(string message)
    {
        return new SoftwareBuildResult(false, null, message);
    }
}

public interface ISoftwareBuildSystem
{
    string Id { get; }

    Task<SoftwareBuildResult> BuildAsync(SoftwareBuildRequest request, CancellationToken cancellationToken);

    Task CleanAsync(SoftwareBuildRequest request, CancellationToken cancellationToken);
}
