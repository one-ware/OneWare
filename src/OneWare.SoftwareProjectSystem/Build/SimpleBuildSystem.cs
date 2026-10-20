using System.Text;
using System.Text.Json;

namespace OneWare.SoftwareProjectSystem.Build;

/// <summary>
/// Builds a project by calling the compiler directly: every source becomes an object file in
/// <c>build/&lt;config&gt;/obj</c>, then everything is linked. Rebuilds are incremental (GCC dependency files and
/// the stored command line) and compile jobs run in parallel. Also writes <c>compile_commands.json</c> for clangd.
/// </summary>
public class SimpleBuildSystem : ISoftwareBuildSystem
{
    public static readonly string[] CExtensions = [".c"];
    public static readonly string[] CxxExtensions = [".cpp", ".cc", ".cxx", ".c++"];
    public static readonly string[] AsmExtensions = [".s", ".S", ".sx"];

    public string Id => "simple";

    public static bool IsSource(string path)
    {
        var extension = Path.GetExtension(path);
        return CExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
               CxxExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
               AsmExtensions.Contains(extension, StringComparer.Ordinal);
    }

    public async Task<SoftwareBuildResult> BuildAsync(SoftwareBuildRequest request,
        CancellationToken cancellationToken)
    {
        var sources = request.Sources.Where(IsSource).ToArray();
        if (sources.Length == 0)
            return SoftwareBuildResult.Failed("No C/C++ source files found in the project.");

        var objectDirectory = Path.Combine(request.BuildDirectory, "obj");
        Directory.CreateDirectory(objectDirectory);

        var jobs = sources.Select(source => CreateCompileJob(request, source, objectDirectory)).ToArray();

        WriteCompileCommands(request, jobs);

        var outdated = jobs.Where(x => IsOutdated(x)).ToArray();
        var failed = 0;

        using (var throttle = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount)))
        {
            var tasks = outdated.Select(async job =>
            {
                await throttle.WaitAsync(cancellationToken);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(job.ObjectFile)!);
                    var success = await RunAsync(request, job.Compiler, job.Arguments, cancellationToken);
                    if (success)
                    {
                        await File.WriteAllTextAsync(job.CommandFile, job.CommandLine, CancellationToken.None);
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                        TryDelete(job.CommandFile);
                    }
                }
                finally
                {
                    throttle.Release();
                }
            });
            await Task.WhenAll(tasks);
        }

        if (failed > 0) return SoftwareBuildResult.Failed($"{failed} file(s) failed to compile.");

        var executable = Path.Combine(request.BuildDirectory, request.OutputName + request.Kit.ExecutableExtension);
        var linker = jobs.Any(x => x.IsCxx) ? request.Kit.CxxCompiler : request.Kit.CCompiler;
        var linkArguments = CreateLinkArguments(request, jobs.Select(x => x.ObjectFile), executable);
        var linkCommandFile = Path.Combine(objectDirectory, "link.cmd");
        var linkCommandLine = JoinCommandLine(linker, linkArguments);

        if (IsLinkOutdated(executable, jobs, linkCommandFile, linkCommandLine))
        {
            if (!await RunAsync(request, linker, linkArguments, cancellationToken))
            {
                TryDelete(linkCommandFile);
                return SoftwareBuildResult.Failed("Linking failed.");
            }

            await File.WriteAllTextAsync(linkCommandFile, linkCommandLine, CancellationToken.None);
        }
        else if (outdated.Length == 0)
        {
            request.Output($"{Path.GetFileName(executable)} is up to date.");
        }

        return new SoftwareBuildResult(true, executable);
    }

    public Task CleanAsync(SoftwareBuildRequest request, CancellationToken cancellationToken)
    {
        if (Directory.Exists(request.BuildDirectory)) Directory.Delete(request.BuildDirectory, true);
        return Task.CompletedTask;
    }

    internal static CompileJob CreateCompileJob(SoftwareBuildRequest request, string relativeSource,
        string objectDirectory)
    {
        var source = Path.GetFullPath(Path.Combine(request.RootDirectory, relativeSource));
        var extension = Path.GetExtension(source);
        var isCxx = CxxExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        var isAsm = AsmExtensions.Contains(extension, StringComparer.Ordinal);

        var objectFile = Path.Combine(objectDirectory, SanitizeRelativePath(relativeSource) + ".o");
        var compiler = isCxx ? request.Kit.CxxCompiler : request.Kit.CCompiler;

        var arguments = new List<string>();

        if (isCxx && !string.IsNullOrWhiteSpace(request.CxxStandard)) arguments.Add($"-std={request.CxxStandard}");
        else if (!isCxx && !isAsm && !string.IsNullOrWhiteSpace(request.CStandard))
            arguments.Add($"-std={request.CStandard}");

        arguments.AddRange(request.IsRelease ? ["-O2", "-DNDEBUG"] : ["-g", "-O0"]);

        foreach (var include in request.IncludeDirectories)
            arguments.Add("-I" + Path.GetFullPath(Path.Combine(request.RootDirectory, include)));
        foreach (var define in request.Defines) arguments.Add("-D" + define);
        arguments.AddRange(request.CompilerFlags);

        arguments.AddRange(["-MMD", "-MP", "-c", source, "-o", objectFile]);

        return new CompileJob(source, objectFile, compiler, arguments, isCxx);
    }

    internal static List<string> CreateLinkArguments(SoftwareBuildRequest request, IEnumerable<string> objects,
        string executable)
    {
        var arguments = new List<string>(objects);
        if (!request.IsRelease) arguments.Add("-g");
        arguments.AddRange(["-o", executable]);
        arguments.AddRange(request.LinkerFlags);
        arguments.AddRange(request.Libraries.Select(x => x.StartsWith('-') ? x : "-l" + x));
        return arguments;
    }

    internal static bool IsOutdated(CompileJob job)
    {
        if (!File.Exists(job.ObjectFile) || !File.Exists(job.CommandFile)) return true;
        if (File.ReadAllText(job.CommandFile) != job.CommandLine) return true;

        var objectTime = File.GetLastWriteTimeUtc(job.ObjectFile);
        if (File.GetLastWriteTimeUtc(job.SourceFile) > objectTime) return true;

        var dependencyFile = Path.ChangeExtension(job.ObjectFile, ".d");
        if (!File.Exists(dependencyFile)) return true;

        foreach (var dependency in ParseDependencyFile(File.ReadAllText(dependencyFile)))
        {
            if (!File.Exists(dependency) || File.GetLastWriteTimeUtc(dependency) > objectTime) return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the prerequisites of the first rule in a Makefile style dependency file written by <c>-MMD</c>.
    /// </summary>
    internal static IReadOnlyList<string> ParseDependencyFile(string content)
    {
        var joined = content.Replace("\\\r\n", " ").Replace("\\\n", " ");
        var firstRule = joined.Split('\n', 2)[0].TrimEnd('\r');

        // The target ends at the first ':' that is followed by whitespace (skips "C:\" drive letters).
        var separator = -1;
        for (var i = 0; i < firstRule.Length - 1; i++)
        {
            if (firstRule[i] == ':' && char.IsWhiteSpace(firstRule[i + 1]))
            {
                separator = i;
                break;
            }
        }

        if (separator < 0) return [];

        var result = new List<string>();
        var current = new StringBuilder();
        var prerequisites = firstRule[(separator + 1)..];
        for (var i = 0; i < prerequisites.Length; i++)
        {
            var c = prerequisites[i];
            if (c == '\\' && i + 1 < prerequisites.Length && prerequisites[i + 1] == ' ')
            {
                current.Append(' ');
                i++;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0) result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    private static bool IsLinkOutdated(string executable, IEnumerable<CompileJob> jobs, string commandFile,
        string commandLine)
    {
        if (!File.Exists(executable) || !File.Exists(commandFile)) return true;
        if (File.ReadAllText(commandFile) != commandLine) return true;
        var executableTime = File.GetLastWriteTimeUtc(executable);
        return jobs.Any(x => File.GetLastWriteTimeUtc(x.ObjectFile) > executableTime);
    }

    private static void WriteCompileCommands(SoftwareBuildRequest request, IEnumerable<CompileJob> jobs)
    {
        var entries = jobs.Select(job => new
        {
            directory = request.RootDirectory,
            file = job.SourceFile,
            output = job.ObjectFile,
            arguments = new[] { job.Compiler }.Concat(job.Arguments).ToArray()
        }).ToArray();

        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(request.CompileCommandsDirectory);
        var path = Path.Combine(request.CompileCommandsDirectory, "compile_commands.json");
        if (!File.Exists(path) || File.ReadAllText(path) != json) File.WriteAllText(path, json);
    }

    private static async Task<bool> RunAsync(SoftwareBuildRequest request, string tool, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        request.Command?.Invoke(JoinCommandLine(tool, arguments));
        try
        {
            var exitCode = await ProcessRunner.RunAsync(tool, arguments, request.RootDirectory, request.Output,
                cancellationToken);
            return exitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            request.Output($"Could not start '{tool}': {e.Message}");
            return false;
        }
    }

    internal static string JoinCommandLine(string tool, IEnumerable<string> arguments)
    {
        return string.Join(' ', new[] { tool }.Concat(arguments).Select(Quote));
    }

    private static string Quote(string argument)
    {
        return argument.Length > 0 && !argument.Any(c => char.IsWhiteSpace(c) || c == '"')
            ? argument
            : "\"" + argument.Replace("\"", "\\\"") + "\"";
    }

    private static string SanitizeRelativePath(string relativePath)
    {
        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x == ".." ? "__" : x);
        return Path.Combine(parts.ToArray());
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

    internal sealed record CompileJob(
        string SourceFile,
        string ObjectFile,
        string Compiler,
        IReadOnlyList<string> Arguments,
        bool IsCxx)
    {
        public string CommandFile => ObjectFile + ".cmd";
        public string CommandLine => JoinCommandLine(Compiler, Arguments);
    }
}
