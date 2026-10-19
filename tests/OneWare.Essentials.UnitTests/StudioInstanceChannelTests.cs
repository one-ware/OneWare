using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OneWare.Essentials.Helpers;
using Xunit;

namespace OneWare.Essentials.UnitTests;

public sealed class StudioInstanceChannelTests : IDisposable
{
    private const int MessageBurst = 20;

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"ow-ipc-{Guid.NewGuid():N}"[..20]);

    private StudioInstanceChannel CreateChannel()
    {
        Directory.CreateDirectory(_directory);
        var pipeName = OperatingSystem.IsWindows()
            ? $"oneware-test-{Guid.NewGuid():N}"
            : Path.Combine(_directory, "ipc.sock");
        return new StudioInstanceChannel(Path.Combine(_directory, "studio.lock"), pipeName);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }
    }

    [Theory]
    [InlineData("shutdown", null)]
    [InlineData("cloud-login", "user-123")]
    [InlineData("cloud-login", "line1\nline2")]
    [InlineData("cloud-login", "")]
    public void FormatCommand_RoundTrips(string name, string? argument)
    {
        var message = StudioInstanceChannel.FormatCommand(name, argument);

        Assert.True(StudioInstanceChannel.TryParseCommand(message, out var parsedName, out var parsedArgument));
        Assert.Equal(name, parsedName);
        Assert.Equal(argument, parsedArgument);
    }

    [Theory]
    [InlineData("/home/user/project.fpgaproj")]
    [InlineData("oneware://open?x=1")]
    [InlineData("activateWindow")]
    [InlineData("cmd:")]
    public void TryParseCommand_RejectsNonCommands(string message)
    {
        Assert.False(StudioInstanceChannel.TryParseCommand(message, out _, out _));
    }

    [Fact]
    public void Lock_IsDetectedWhileHeldAndReleasedOnDispose()
    {
        var channel = CreateChannel();
        Assert.False(channel.IsStudioRunning());

        var primaryLock = channel.TryAcquirePrimaryLock();
        Assert.NotNull(primaryLock);
        Assert.True(channel.IsStudioRunning());
        Assert.Null(channel.TryAcquirePrimaryLock());

        primaryLock.Dispose();
        Assert.False(channel.IsStudioRunning());
    }

    [Fact]
    public async Task WaitForExit_ReturnsFalseWhileLockIsHeld()
    {
        var channel = CreateChannel();
        using var primaryLock = channel.TryAcquirePrimaryLock();

        Assert.False(await channel.WaitForExitAsync(TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public async Task SendCommand_ReachesServer()
    {
        var channel = CreateChannel();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var messages = new List<string>();
        var server = Task.Run(() => channel.RunServerAsync(message =>
        {
            lock (messages)
            {
                messages.Add(message);
                if (messages.Count == MessageBurst + 1)
                    received.TrySetResult(message);
            }
        }, cancellationToken: cts.Token));

        Assert.True(await SendWithRetryAsync(channel, () => channel.SendCommandAsync("cloud-login", "user-1")));
        for (var i = 0; i < MessageBurst; i++)
            Assert.True(await channel.SendMessageAsync($"/tmp/project{i}"));

        await received.Task.WaitAsync(cts.Token);
        Assert.Equal(StudioInstanceChannel.FormatCommand("cloud-login", "user-1"), messages[0]);
        Assert.Equal(MessageBurst + 1, messages.Count);

        await cts.CancelAsync();
        await server;
    }

    [Fact]
    public async Task SendCommand_ReturnsFalseWithoutServer()
    {
        var channel = CreateChannel();

        Assert.False(await channel.SendCommandAsync(StudioInstanceChannel.ShutdownCommand));
    }

    private static async Task<bool> SendWithRetryAsync(StudioInstanceChannel channel, Func<Task<bool>> send)
    {
        // The server may not be listening yet.
        for (var i = 0; i < 20; i++)
        {
            if (await send())
                return true;
            await Task.Delay(100);
        }

        return false;
    }
}
