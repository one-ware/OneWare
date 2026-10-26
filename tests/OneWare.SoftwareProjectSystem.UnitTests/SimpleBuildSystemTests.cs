using System.Diagnostics;
using System.Text.Json;
using OneWare.Essentials.Helpers;
using OneWare.SoftwareProjectSystem.Build;
using OneWare.SoftwareProjectSystem.Services;
using Xunit;

namespace OneWare.SoftwareProjectSystem.UnitTests;

public sealed class SimpleBuildSystemTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "oneware-swproj-" + Guid.NewGuid().ToString("N"));

    public SimpleBuildSystemTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void ParsesDependencyFiles()
    {
        var content = "obj/main.cpp.o: /src/main.cpp /src/a\\ b.h \\\n /src/c.h\n\n/src/a\\ b.h:\n\n/src/c.h:\n";
        Assert.Equal(["/src/main.cpp", "/src/a b.h", "/src/c.h"], SimpleBuildSystem.ParseDependencyFile(content));
    }

    [Fact]
    public void ParsesDependencyFilesWithDriveLetters()
    {
        var content = @"C:\p\build\obj\main.c.o: C:\p\main.c C:\p\inc.h" + "\n";
        Assert.Equal([@"C:\p\main.c", @"C:\p\inc.h"], SimpleBuildSystem.ParseDependencyFile(content));
    }

    [Fact]
    public void CreatesCompileArguments()
    {
        var request = CreateRequest(KitFor("gcc", "g++"), ["src/main.cpp"]) with
        {
            CxxStandard = "c++20",
            IncludeDirectories = ["include"],
            Defines = ["FOO=1"],
            CompilerFlags = ["-Wall"]
        };

        var job = SimpleBuildSystem.CreateCompileJob(request, "src/main.cpp", Path.Combine(_root, "obj"));

        Assert.Equal("g++", job.Compiler);
        Assert.True(job.IsCxx);
        Assert.Contains("-std=c++20", job.Arguments);
        Assert.Contains("-g", job.Arguments);
        Assert.Contains("-O0", job.Arguments);
        Assert.Contains("-I" + Path.Combine(_root, "include"), job.Arguments);
        Assert.Contains("-DFOO=1", job.Arguments);
        Assert.Contains("-Wall", job.Arguments);
        Assert.Equal(Path.Combine(_root, "obj", "src", "main.cpp.o"), job.ObjectFile);
    }

    [Fact]
    public async Task BuildsRunsAndRebuildsIncrementally()
    {
        if (PlatformHelper.GetFullPath("gcc" + PlatformHelper.ExecutableExtension) is not { } gcc ||
            PlatformHelper.GetFullPath("g++" + PlatformHelper.ExecutableExtension) is not { } gxx)
            return;

        Directory.CreateDirectory(Path.Combine(_root, "src"));
        await File.WriteAllTextAsync(Path.Combine(_root, "src", "util.h"), "int answer();\n");
        await File.WriteAllTextAsync(Path.Combine(_root, "src", "util.c"),
            "#include \"util.h\"\nint answer() { return 42; }\n");
        await File.WriteAllTextAsync(Path.Combine(_root, "src", "main.cpp"),
            "#include <cstdio>\nextern \"C\" {\n#include \"util.h\"\n}\nint main() { std::printf(\"%d\\n\", answer()); }\n");

        var commands = new List<string>();
        var request = CreateRequest(KitFor(gcc, gxx), ["src/main.cpp", "src/util.c"]) with
        {
            Command = commands.Add
        };

        var system = new SimpleBuildSystem();
        var result = await system.BuildAsync(request, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(result.Executable));
        Assert.Equal("42", RunProgram(result.Executable!));
        Assert.Equal(3, commands.Count);

        var compileCommands = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(_root, "build", "compile_commands.json")));
        Assert.Equal(2, compileCommands.RootElement.GetArrayLength());

        // Nothing changed -> nothing runs.
        commands.Clear();
        result = await system.BuildAsync(request, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Empty(commands);

        // A header change recompiles its dependents and relinks.
        await Task.Delay(1100);
        await File.AppendAllTextAsync(Path.Combine(_root, "src", "util.h"), "// changed\n");
        result = await system.BuildAsync(request, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(3, commands.Count);

        // A flag change recompiles everything.
        commands.Clear();
        result = await system.BuildAsync(request with { Defines = ["X"] }, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(3, commands.Count);
    }

    [Fact]
    public async Task ReportsCompilerErrors()
    {
        if (PlatformHelper.GetFullPath("gcc" + PlatformHelper.ExecutableExtension) is not { } gcc) return;

        await File.WriteAllTextAsync(Path.Combine(_root, "main.c"), "int main() { return y; }\n");

        var output = new List<string>();
        var request = CreateRequest(KitFor(gcc, gcc), ["main.c"]) with { Output = line => { lock (output) output.Add(line); } };

        var result = await new SimpleBuildSystem().BuildAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        var diagnostics = output
            .Select(line => GccDiagnosticParser.TryParse(line, _root, out var d) ? d : null)
            .OfType<CompilerDiagnostic>()
            .ToList();
        var error = Assert.Single(diagnostics, x => x.Type == Essentials.Enums.ErrorType.Error);
        Assert.Equal(Path.Combine(_root, "main.c"), error.FilePath);
        Assert.Equal(1, error.Line);
    }

    [Fact]
    public async Task FailsWithoutSources()
    {
        var result = await new SimpleBuildSystem().BuildAsync(CreateRequest(KitFor("gcc", "g++"), []),
            CancellationToken.None);
        Assert.False(result.Success);
    }

    private SoftwareBuildRequest CreateRequest(ToolchainKit kit, IReadOnlyList<string> sources)
    {
        return new SoftwareBuildRequest
        {
            RootDirectory = _root,
            BuildDirectory = Path.Combine(_root, "build", "Debug"),
            CompileCommandsDirectory = Path.Combine(_root, "build"),
            Kit = kit,
            Configuration = "Debug",
            OutputName = "app",
            Sources = sources,
            Output = _ => { }
        };
    }

    private static ToolchainKit KitFor(string cc, string cxx)
    {
        return new ToolchainKit("test", "Test", cc, cxx)
        {
            ExecutableExtension = OperatingSystem.IsWindows() ? ".exe" : string.Empty
        };
    }

    private static string RunProgram(string path)
    {
        using var process = Process.Start(new ProcessStartInfo(path) { RedirectStandardOutput = true })!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }
}
