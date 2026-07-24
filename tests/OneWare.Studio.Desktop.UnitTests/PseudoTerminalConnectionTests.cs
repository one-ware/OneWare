using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneWare.Terminal;
using OneWare.Terminal.Provider;
using OneWare.Terminal.Provider.Unix;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class ShellIntegrationParserTests
{
    private static string FeedText(ShellIntegrationParser parser, string input,
        List<ShellIntegrationEvent>? events = null)
    {
        var text = new StringBuilder();
        foreach (var segment in parser.Feed(Encoding.UTF8.GetBytes(input)))
        {
            if (segment.Data != null) text.Append(Encoding.UTF8.GetString(segment.Data));
            else if (segment.Event is { } e) events?.Add(e);
        }

        return text.ToString();
    }

    [Fact]
    public void Feed_StripsIntegrationSequencesAndRaisesEvents()
    {
        var parser = new ShellIntegrationParser();
        var events = new List<ShellIntegrationEvent>();

        var text = FeedText(parser, "before\u001b]633;C\u0007output\u001b]633;D;3\u0007after", events);

        Assert.Equal("beforeoutputafter", text);
        Assert.Equal(2, events.Count);
        Assert.Equal('C', events[0].Command);
        Assert.Equal('D', events[1].Command);
        Assert.Equal("3", events[1].Argument);
    }

    [Fact]
    public void Feed_HandlesSequencesSplitAcrossChunks()
    {
        var parser = new ShellIntegrationParser();
        var events = new List<ShellIntegrationEvent>();
        var input = "abc\u001b]633;D;127\u0007def";

        var text = new StringBuilder();
        foreach (var chunk in input.Select(c => c.ToString()))
            text.Append(FeedText(parser, chunk, events));

        Assert.Equal("abcdef", text.ToString());
        var integrationEvent = Assert.Single(events);
        Assert.Equal('D', integrationEvent.Command);
        Assert.Equal("127", integrationEvent.Argument);
    }

    [Fact]
    public void Feed_SupportsStringTerminator()
    {
        var parser = new ShellIntegrationParser();
        var events = new List<ShellIntegrationEvent>();

        var text = FeedText(parser, "x\u001b]633;D;0\u001b\\y", events);

        Assert.Equal("xy", text);
        var integrationEvent = Assert.Single(events);
        Assert.Equal('D', integrationEvent.Command);
        Assert.Equal("0", integrationEvent.Argument);
    }

    [Fact]
    public void Feed_PassesOtherOscSequencesThrough()
    {
        var parser = new ShellIntegrationParser();
        var events = new List<ShellIntegrationEvent>();
        const string title = "pre\u001b]0;window title\u0007post";

        var text = FeedText(parser, title, events);

        Assert.Equal(title, text);
        Assert.Empty(events);
    }

    [Fact]
    public void Feed_PassesCsiAndPlainEscapesThrough()
    {
        var parser = new ShellIntegrationParser();
        const string input = "a\u001b[31mred\u001b[0m\u001b(Bb";

        var text = FeedText(parser, input);

        Assert.Equal(input, text);
    }
}

public class PseudoTerminalConnectionTests
{
    [Fact]
    public async Task ShellIntegration_ReportsCommandLifecycleOnUnix()
    {
        if (OperatingSystem.IsWindows()) return;

        var bash = "/bin/bash";
        var config = ShellIntegration.GetSpawnConfig(bash);
        Assert.NotNull(config.Arguments);

        var provider = new UnixPseudoTerminalProvider();
        using var terminal = provider.Create(80, 24, Path.GetTempPath(), bash, config.Environment,
            config.Arguments);
        Assert.NotNull(terminal);

        using var connection = new PseudoTerminalConnection(terminal);
        var output = new StringBuilder();
        var outputLock = new object();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.DataReceived += (_, args) =>
        {
            lock (outputLock)
                output.Append(Encoding.UTF8.GetString(args.Data));
        };
        connection.IntegrationEvent += (_, args) =>
        {
            if (args.IsCommandStarted)
                started.TrySetResult();
            else if (args.IsCommandCompleted && started.Task.IsCompleted)
                completed.TrySetResult(args.ExitCode);
        };
        connection.Connect();

        connection.SendData(Encoding.UTF8.GetBytes("printf 'integration-output\\n'; exit_test() { return 0; }\r"));

        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var exitCode = await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, exitCode);
        string text;
        lock (outputLock)
            text = output.ToString();
        Assert.Contains("integration-output", text);
        Assert.DoesNotContain("633;", text);
        // The PS0 hook must not leak readline prompt markers (\x01/\x02) into the output.
        Assert.DoesNotContain('\u0001', text);
        Assert.DoesNotContain('\u0002', text);
    }

    [Fact]
    public async Task ShellIntegration_ReportsFailingExitCodeOnUnix()
    {
        if (OperatingSystem.IsWindows()) return;

        var bash = "/bin/bash";
        var config = ShellIntegration.GetSpawnConfig(bash);

        var provider = new UnixPseudoTerminalProvider();
        using var terminal = provider.Create(80, 24, Path.GetTempPath(), bash, config.Environment,
            config.Arguments);
        Assert.NotNull(terminal);

        using var connection = new PseudoTerminalConnection(terminal);
        var started = false;
        var completed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.IntegrationEvent += (_, args) =>
        {
            if (args.IsCommandStarted)
                started = true;
            else if (args.IsCommandCompleted && started)
                completed.TrySetResult(args.ExitCode);
        };
        connection.Connect();

        connection.SendData(Encoding.UTF8.GetBytes("exit_code_test() { return 42; }; exit_code_test\r"));

        var exitCode = await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(42, exitCode);
    }
}
