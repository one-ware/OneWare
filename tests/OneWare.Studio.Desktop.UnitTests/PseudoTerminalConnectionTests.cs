using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OneWare.Terminal.Provider;
using OneWare.Terminal.Provider.Unix;
using OneWare.Terminal.Provider.Win32;
using OneWare.Terminal.ViewModels;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class PseudoTerminalConnectionTests
{
    private const string MarkerCommand =
        "__ow_exit=$?; printf '\\033[1A\\r\\033[2K'; printf '0123456789abcdef:%s\\n' \"$__ow_exit\" >&198; " +
        "IFS= read -r __ow_ack <&199";

    [Fact]
    public void FilterOutput_SuppressesSequenceAcrossChunks()
    {
        var connection = new PseudoTerminalConnection(null!);
        var marker = Encoding.ASCII.GetBytes(MarkerCommand);
        connection.SuppressOutput(marker);

        var first = connection.FilterOutput(Encoding.ASCII.GetBytes($"before{MarkerCommand[..30]}"));
        var second = connection.FilterOutput(Encoding.ASCII.GetBytes($"{MarkerCommand[30..]}after"));

        Assert.Equal("before", Encoding.ASCII.GetString(first));
        Assert.Equal("after", Encoding.ASCII.GetString(second));
    }

    [Fact]
    public void FilterOutput_SuppressesWrappedReadlineEcho()
    {
        var connection = new PseudoTerminalConnection(null!);
        connection.SuppressOutput(Encoding.ASCII.GetBytes(MarkerCommand));

        var wrappedEcho = MarkerCommand
            .Replace("\\033[1A", "\\\\\r\\033[1A")
            .Replace("$__ow_exit\"", "$__ow_ex\rxit\"");
        var first = connection.FilterOutput(Encoding.ASCII.GetBytes($"prompt{wrappedEcho[..48]}"));
        var second = connection.FilterOutput(Encoding.ASCII.GetBytes(wrappedEcho[48..]));

        Assert.Equal("prompt", Encoding.ASCII.GetString(first));
        Assert.Empty(second);
    }

    [Fact]
    public void FilterOutput_ReleasesIncompleteSuppressionAtLineEnd()
    {
        var connection = new PseudoTerminalConnection(null!);
        connection.SuppressOutput(Encoding.ASCII.GetBytes(MarkerCommand));

        var output = connection.FilterOutput(Encoding.ASCII.GetBytes("__ow_broken\r\nnext"));

        Assert.Equal("__ow_broken\r\nnext", Encoding.ASCII.GetString(output));
    }

    [Fact]
    public void FilterOutput_UsesIndependentStateForSeparateConsumers()
    {
        var marker = Encoding.ASCII.GetBytes(MarkerCommand);
        var terminalFilter = new OutputSequenceSuppressor();
        var chatFilter = new OutputSequenceSuppressor();
        terminalFilter.SuppressOutput(marker);
        chatFilter.SuppressOutput(marker);
        var rawOutput = Encoding.ASCII.GetBytes($"before{MarkerCommand}after");

        var terminalOutput = terminalFilter.FilterOutput(rawOutput);
        var chatOutput = chatFilter.FilterOutput(rawOutput);

        Assert.Equal("beforeafter", Encoding.ASCII.GetString(terminalOutput));
        Assert.Equal("beforeafter", Encoding.ASCII.GetString(chatOutput));
    }

    [Fact]
    public async Task ControlChannel_CompletesUnixCommandOutOfBand()
    {
        if (OperatingSystem.IsWindows()) return;

        var provider = new UnixPseudoTerminalProvider();
        using var terminal = provider.Create(80, 24, Path.GetTempPath(), "/bin/bash", null,
            "--noprofile --norc");
        Assert.NotNull(terminal);

        using var connection = new PseudoTerminalConnection(terminal);
        var output = new StringBuilder();
        var completion = new TaskCompletionSource<TerminalCommandCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.DataReceived += (_, args) => output.Append(Encoding.UTF8.GetString(args.Data));
        connection.CommandCompleted += (_, args) => completion.TrySetResult(args);
        connection.Connect();

        const string executionId = "integration";
        var setup = "__owc(){ __ow_exit=$?; printf '\\033[1A\\r\\033[2K'; " +
                    "printf '%s:%s\\n' \"$1\" \"$__ow_exit\" >&198; IFS= read -r __ow_ack <&199; }\r";
        connection.SendData(Encoding.ASCII.GetBytes(setup));
        await Task.Delay(100);

        var command = $"printf 'control-channel-output\\n'\n__owc {executionId}\r";
        connection.SendData(Encoding.ASCII.GetBytes(command));

        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(executionId, result.ExecutionId);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("control-channel-output", output.ToString());
        Assert.DoesNotContain("OW_DONE", output.ToString());
    }

    [Fact]
    public async Task ControlChannel_CompletesWindowsCommandOutOfBand()
    {
        if (!OperatingSystem.IsWindows()) return;

        var terminalViewModel = new TerminalViewModel(Path.GetTempPath());
        var startArguments = terminalViewModel.StartArguments!;
        startArguments = startArguments.Insert(startArguments.Length - 1,
            "; $abandoned=[System.IO.Pipes.NamedPipeClientStream]::new(" +
            "'.',$env:OW_CONTROL_PIPE,[System.IO.Pipes.PipeDirection]::InOut); " +
            "$abandoned.Connect(); $abandoned.Dispose(); " +
            "__owc 'integration-1' $true 0; __owc 'integration-2' $false 7");
        var provider = new Win32ConPtyPseudoTerminalProvider();
        using var terminal = provider.Create(80, 24, Path.GetTempPath(), "powershell.exe", null,
            startArguments);
        Assert.NotNull(terminal);

        try
        {
            using var connection = new PseudoTerminalConnection(terminal);
            var firstCompletion = new TaskCompletionSource<TerminalCommandCompletedEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var secondCompletion = new TaskCompletionSource<TerminalCommandCompletedEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            connection.CommandCompleted += (_, args) =>
            {
                if (args.ExecutionId == "integration-1")
                    firstCompletion.TrySetResult(args);
                else if (args.ExecutionId == "integration-2")
                    secondCompletion.TrySetResult(args);
            };
            connection.Connect();

            var firstResult = await firstCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var secondResult = await secondCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(0, firstResult.ExitCode);
            Assert.Equal(7, secondResult.ExitCode);
        }
        finally
        {
            if (!terminal.Process.HasExited)
                terminal.Process.Kill(true);
        }
    }

    [Fact]
    public async Task SendData_ReportsUserInterrupt()
    {
        if (OperatingSystem.IsWindows()) return;

        var provider = new UnixPseudoTerminalProvider();
        using var terminal = provider.Create(80, 24, Path.GetTempPath(), "/bin/bash", null,
            "--noprofile --norc");
        Assert.NotNull(terminal);

        using var connection = new PseudoTerminalConnection(terminal);
        var interrupted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.UserInterrupted += (_, _) => interrupted.TrySetResult();
        connection.Connect();
        connection.SendData([0x03]);

        await interrupted.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
