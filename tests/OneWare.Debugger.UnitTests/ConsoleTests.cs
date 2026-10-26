using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.Models;
using OneWare.Debugger.ViewModels.Main;
using OneWare.Essentials.Debugger.Interfaces;
using Xunit;

namespace OneWare.Debugger.UnitTests;

public class ConsoleTests
{
    [Theory]
    [InlineData("(gdb)")]
    [InlineData("^running")]
    [InlineData("*stopped,reason=\"breakpoint-hit\",bkptno=\"1\"")]
    [InlineData("=thread-group-started,id=\"i1\"")]
    [InlineData("12^done")]
    public void MiRecordsAreProtocol(string text)
    {
        Assert.Equal(ConsoleLineKind.Protocol, ConsoleLineClassifier.Classify(text, true));
    }

    [Theory]
    [InlineData("*** stack smashing detected ***")]
    [InlineData("Hello world")]
    [InlineData("^ not a record")]
    public void ProgramOutputIsOutput(string text)
    {
        Assert.Equal(ConsoleLineKind.Output, ConsoleLineClassifier.Classify(text, false));
    }

    [Fact]
    public void ErrorsAreOnlyReportedForUserCommands()
    {
        Assert.Equal(ConsoleLineKind.Error, ConsoleLineClassifier.Classify("error: No symbol \"x\".", true));
        Assert.Equal(ConsoleLineKind.Protocol, ConsoleLineClassifier.Classify("error: No symbol \"x\".", false));
        Assert.Equal(ConsoleLineKind.Error, ConsoleLineClassifier.Classify(ConsoleLineClassifier.TimeoutMessage, false));
        Assert.Equal(ConsoleLineKind.Warning, ConsoleLineClassifier.Classify("warning: no symbols", false));
    }

    [Fact]
    public async Task UserCommandEchoIsDroppedAndDuplicateErrorMerged()
    {
        var console = CreateConsole();

        console.CommandText = "print x";
        await console.SendCommandCommand.ExecuteAsync(null);

        console.ReceiveCommand("print x");
        console.ReceiveOutput("print x");
        console.ReceiveOutput("No symbol \"x\" in current context.");
        console.ReceiveOutput("error: No symbol \"x\" in current context.");

        Assert.Equal(
            [
                ("print x", ConsoleLineKind.Command),
                ("error: No symbol \"x\" in current context.", ConsoleLineKind.Error)
            ],
            console.Lines.Select(x => (x.Text, x.Kind)));
    }

    [Fact]
    public void InternalTrafficIsHiddenUntilProtocolIsShown()
    {
        var console = CreateConsole();

        console.ReceiveCommand("-stack-list-locals --simple-values");
        console.ReceiveOutput("error: No frame selected.");
        console.ReceiveOutput("(gdb)");
        console.ReceiveOutput("Hello");

        Assert.Equal(["Hello"], console.Lines.Select(x => x.Text));

        console.ShowProtocol = true;
        Assert.Equal(4, console.Lines.Count);
        Assert.All(console.Lines.Take(3), x => Assert.True(x.IsProtocol));

        console.ShowProtocol = false;
        Assert.Single(console.Lines);
    }

    [Fact]
    public async Task HistoryBrowsesLikeAShell()
    {
        var console = CreateConsole();

        foreach (var command in new[] { "info registers", "bt" })
        {
            console.CommandText = command;
            await console.SendCommandCommand.ExecuteAsync(null);
        }

        console.BrowseHistory(-1);
        Assert.Equal("bt", console.CommandText);
        console.BrowseHistory(-1);
        Assert.Equal("info registers", console.CommandText);
        console.BrowseHistory(-1);
        Assert.Equal("info registers", console.CommandText);
        console.BrowseHistory(1);
        console.BrowseHistory(1);
        Assert.Equal(string.Empty, console.CommandText);
    }

    [Fact]
    public void OldestLinesAreDroppedBeyondTheLimit()
    {
        var console = CreateConsole();

        for (var i = 0; i < ConsoleTabViewModel.MaxLines + 10; i++) console.ReceiveOutput($"line {i}");

        Assert.Equal(ConsoleTabViewModel.MaxLines, console.Lines.Count);
        Assert.Equal("line 10", console.Lines[0].Text);
    }

    private static ConsoleTabViewModel CreateConsole()
    {
        return new ConsoleTabViewModel(DispatchProxy.Create<IDebuggerService, CompletingProxy>())
        {
            IsSessionActive = true
        };
    }

    public class CompletingProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var type = targetMethod?.ReturnType;
            if (type == typeof(Task)) return Task.CompletedTask;
            return type is { IsValueType: true } && type != typeof(void) ? Activator.CreateInstance(type) : null;
        }
    }
}
