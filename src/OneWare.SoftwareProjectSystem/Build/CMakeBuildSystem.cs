using System.Text.Json;
using OneWare.Essentials.Helpers;

namespace OneWare.SoftwareProjectSystem.Build;

/// <summary>
/// Builds projects that bring their own <c>CMakeLists.txt</c>. Uses Ninja when available and reads the produced
/// executables from the CMake File API.
/// </summary>
public class CMakeBuildSystem : ISoftwareBuildSystem
{
    private const string KitMarkerFile = "oneware-kit.txt";

    private readonly Func<string, string?> _resolveExecutable;

    public CMakeBuildSystem() : this(name => PlatformHelper.GetFullPath(name + PlatformHelper.ExecutableExtension))
    {
    }

    public CMakeBuildSystem(Func<string, string?> resolveExecutable)
    {
        _resolveExecutable = resolveExecutable;
    }

    public string Id => "cmake";

    public async Task<SoftwareBuildResult> BuildAsync(SoftwareBuildRequest request,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(Path.Combine(request.RootDirectory, "CMakeLists.txt")))
            return SoftwareBuildResult.Failed("CMakeLists.txt not found in the project root.");

        if (_resolveExecutable("cmake") is not { } cmake)
            return SoftwareBuildResult.Failed("CMake was not found on PATH. Install CMake or switch the project to the simple build system.");

        var buildDirectory = request.BuildDirectory;
        var kitMarker = Path.Combine(buildDirectory, KitMarkerFile);
        var kitDescription = $"{request.Kit.Id}\n{request.Kit.CCompiler}\n{request.Kit.CxxCompiler}\n" +
                             string.Join('\n', request.CMakeArguments);

        if (File.Exists(kitMarker) && File.ReadAllText(kitMarker) != kitDescription)
        {
            // Changing compilers requires a fresh cache.
            request.Output("Toolchain changed, reconfiguring from scratch.");
            Directory.Delete(buildDirectory, true);
        }

        Directory.CreateDirectory(buildDirectory);
        var queryDirectory = Path.Combine(buildDirectory, ".cmake", "api", "v1", "query");
        Directory.CreateDirectory(queryDirectory);
        var query = Path.Combine(queryDirectory, "codemodel-v2");
        if (!File.Exists(query)) await File.WriteAllTextAsync(query, string.Empty, cancellationToken);

        if (!File.Exists(Path.Combine(buildDirectory, "CMakeCache.txt")))
        {
            var configure = new List<string>
            {
                "-S", request.RootDirectory,
                "-B", buildDirectory,
                $"-DCMAKE_BUILD_TYPE={request.Configuration}",
                "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON",
                $"-DCMAKE_C_COMPILER={request.Kit.CCompiler}",
                $"-DCMAKE_CXX_COMPILER={request.Kit.CxxCompiler}"
            };

            if (!request.Kit.IsHost)
                configure.AddRange(["-DCMAKE_SYSTEM_NAME=Generic", "-DCMAKE_TRY_COMPILE_TARGET_TYPE=STATIC_LIBRARY"]);

            if (_resolveExecutable("ninja") is { } ninja)
                configure.AddRange(["-G", "Ninja", $"-DCMAKE_MAKE_PROGRAM={ninja}"]);

            configure.AddRange(request.CMakeArguments);

            if (!await RunAsync(request, cmake, configure, cancellationToken))
            {
                // Don't keep a half configured cache around, the next build would skip configuring.
                TryDelete(Path.Combine(buildDirectory, "CMakeCache.txt"));
                return SoftwareBuildResult.Failed("CMake configuration failed.");
            }

            await File.WriteAllTextAsync(kitMarker, kitDescription, cancellationToken);
        }

        var build = new List<string> { "--build", buildDirectory, "--parallel" };
        if (!await RunAsync(request, cmake, build, cancellationToken))
            return SoftwareBuildResult.Failed("Build failed.");

        CopyCompileCommands(request);

        var executable = FindExecutable(buildDirectory, request.HasExplicitOutputName ? request.OutputName : null);
        return new SoftwareBuildResult(true, executable,
            executable == null ? "The build succeeded but produced no executable target." : null);
    }

    public Task CleanAsync(SoftwareBuildRequest request, CancellationToken cancellationToken)
    {
        if (Directory.Exists(request.BuildDirectory)) Directory.Delete(request.BuildDirectory, true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads the executable targets from the CMake File API reply. Prefers <paramref name="preferredTarget" />.
    /// </summary>
    internal static string? FindExecutable(string buildDirectory, string? preferredTarget)
    {
        try
        {
            var replyDirectory = Path.Combine(buildDirectory, ".cmake", "api", "v1", "reply");
            if (!Directory.Exists(replyDirectory)) return null;

            var index = Directory.GetFiles(replyDirectory, "index-*.json").OrderDescending().FirstOrDefault();
            if (index == null) return null;

            using var indexDocument = JsonDocument.Parse(File.ReadAllText(index));
            var codemodelFile = indexDocument.RootElement.GetProperty("reply").GetProperty("codemodel-v2")
                .GetProperty("jsonFile").GetString();
            if (codemodelFile == null) return null;

            using var codemodel = JsonDocument.Parse(File.ReadAllText(Path.Combine(replyDirectory, codemodelFile)));
            var executables = new List<(string Name, string Path)>();

            foreach (var configuration in codemodel.RootElement.GetProperty("configurations").EnumerateArray())
            foreach (var target in configuration.GetProperty("targets").EnumerateArray())
            {
                var targetFile = target.GetProperty("jsonFile").GetString();
                if (targetFile == null) continue;

                using var targetDocument =
                    JsonDocument.Parse(File.ReadAllText(Path.Combine(replyDirectory, targetFile)));
                var root = targetDocument.RootElement;
                if (root.GetProperty("type").GetString() != "EXECUTABLE") continue;
                if (!root.TryGetProperty("artifacts", out var artifacts)) continue;

                foreach (var artifact in artifacts.EnumerateArray())
                {
                    var path = artifact.GetProperty("path").GetString();
                    if (path == null) continue;
                    var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(buildDirectory, path);
                    // On Windows the first artifact is the .exe, followed by the .pdb.
                    executables.Add((root.GetProperty("name").GetString() ?? string.Empty, fullPath));
                    break;
                }
            }

            if (preferredTarget != null &&
                executables.FirstOrDefault(x => x.Name == preferredTarget) is { Path: not null } preferred)
                return preferred.Path;

            return executables.Select(x => x.Path).FirstOrDefault();
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException
                                       or InvalidOperationException)
        {
            return null;
        }
    }

    private static void CopyCompileCommands(SoftwareBuildRequest request)
    {
        var source = Path.Combine(request.BuildDirectory, "compile_commands.json");
        if (!File.Exists(source)) return;
        Directory.CreateDirectory(request.CompileCommandsDirectory);
        var destination = Path.Combine(request.CompileCommandsDirectory, "compile_commands.json");
        if (Path.GetFullPath(source) == Path.GetFullPath(destination)) return;
        File.Copy(source, destination, true);
    }

    private static async Task<bool> RunAsync(SoftwareBuildRequest request, string tool, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        request.Command?.Invoke(SimpleBuildSystem.JoinCommandLine(tool, arguments));
        try
        {
            return await ProcessRunner.RunAsync(tool, arguments, request.RootDirectory, request.Output,
                cancellationToken) == 0;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            request.Output($"Could not start '{tool}': {e.Message}");
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
