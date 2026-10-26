using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Helpers;
using Xunit;

namespace OneWare.Debugger.UnitTests;

/// <summary>
///     Runs a real local GDB session (skipped when gcc or gdb is missing). The project folder is reached
///     through a symbolic link, the way home folders often are, because GDB reports such paths resolved.
/// </summary>
public sealed class GdbSessionIntegrationTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "oneware-gdb-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_base, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task StopsAtBreakpointInLinkedFolder()
    {
        if (OperatingSystem.IsWindows()) return;
        if (PlatformHelper.GetFullPath("gcc") is not { } gcc || PlatformHelper.GetFullPath("gdb") is not { } gdb)
            return;

        var real = Path.Combine(_base, "real");
        var linked = Path.Combine(_base, "linked");
        Directory.CreateDirectory(real);
        Directory.CreateSymbolicLink(linked, real);

        var source = Path.Combine(linked, "main.c");
        await File.WriteAllTextAsync(source, "int main(void)\n{\n    int x = 1;\n    x++;\n    return x - 2;\n}\n");
        var program = Path.Combine(linked, "main.out");

        using (var compile = Process.Start(gcc, ["-g", "-O0", source, "-o", program]))
        {
            await compile.WaitForExitAsync();
            Assert.Equal(0, compile.ExitCode);
        }

        var session = new GdbSession(gdb, program, null, linked, null, true, NullLogger.Instance);
        var stopped = new TaskCompletionSource<DebugSessionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.StateChanged += (_, state) =>
        {
            if (state is { IsRunning: false, CurrentFrame.Line: > 0 }) stopped.TrySetResult(state);
        };

        try
        {
            Assert.True(await session.StartAsync());

            var sources = await session.GetSourceFilesAsync();
            Assert.NotNull(sources);
            Assert.Contains(source, sources);

            Assert.True(await session.SetBreakpointAsync(new BreakPoint { File = source, Line = 4 }));

            await session.RunAsync();

            var state = await stopped.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(4, state.CurrentFrame!.Line);
            Assert.Equal(source, state.CurrentFrame.File);
        }
        finally
        {
            session.Stop();
        }
    }

    [Fact]
    public async Task EvaluatesStructuresForHover()
    {
        if (OperatingSystem.IsWindows()) return;
        if (PlatformHelper.GetFullPath("gcc") is not { } gcc || PlatformHelper.GetFullPath("gdb") is not { } gdb)
            return;

        Directory.CreateDirectory(_base);
        var source = Path.Combine(_base, "main.c");
        await File.WriteAllTextAsync(source,
            "struct pos { int x; int y; };\nstruct item { struct pos p; int v[3]; };\n" +
            "int main(void)\n{\n    struct item it = { { 1, 2 }, { 4, 5, 6 } };\n    return it.p.y - 2;\n}\n");
        var program = Path.Combine(_base, "main.out");

        using (var compile = Process.Start(gcc, ["-g", "-O0", source, "-o", program]))
        {
            await compile.WaitForExitAsync();
            Assert.Equal(0, compile.ExitCode);
        }

        var session = new GdbSession(gdb, program, null, _base, null, true, NullLogger.Instance);
        var stopped = new TaskCompletionSource<DebugSessionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.StateChanged += (_, state) =>
        {
            if (state is { IsRunning: false, CurrentFrame.Line: > 0 }) stopped.TrySetResult(state);
        };

        try
        {
            Assert.True(await session.StartAsync());
            Assert.True(await session.SetBreakpointAsync(new BreakPoint { File = source, Line = 6 }));
            await session.RunAsync();

            var state = await stopped.Task.WaitAsync(TimeSpan.FromSeconds(20));

            // Locals come with simple values only -> structures have to be evaluated.
            var local = Assert.Single(state.Locals, x => x.Name == "it");
            Assert.True(string.IsNullOrEmpty(local.Value));

            var value = await session.EvaluateExpressionAsync("it");
            Assert.NotNull(value);

            var node = Helpers.GdbValueParser.Parse("it", value);
            Assert.Equal(["p", "v"], node.Children.Select(x => x.Name));
            Assert.Equal(["[0]", "[1]", "[2]"], node.Children[1].Children.Select(x => x.Name));

            Assert.Equal("2", await session.EvaluateExpressionAsync("it.p.y"));
            Assert.Null(await session.EvaluateExpressionAsync("does_not_exist"));
        }
        finally
        {
            session.Stop();
        }
    }
}
