using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OneWare.Core.Services;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class IpcCommandServiceTests
{
    [Fact]
    public async Task ExecuteAsync_DispatchesToRegisteredHandler()
    {
        var service = new IpcCommandService(NullLogger.Instance);
        string? received = null;
        service.RegisterCommand("cloud-login", argument =>
        {
            received = argument;
            return Task.CompletedTask;
        });

        Assert.True(await service.ExecuteAsync("CLOUD-LOGIN", "user-1"));
        Assert.Equal("user-1", received);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFalseForUnknownCommand()
    {
        var service = new IpcCommandService(NullLogger.Instance);

        Assert.False(await service.ExecuteAsync("missing", null));
    }

    [Fact]
    public async Task ExecuteAsync_SwallowsHandlerExceptions()
    {
        var service = new IpcCommandService(NullLogger.Instance);
        service.RegisterCommand("fail", _ => throw new InvalidOperationException());

        Assert.True(await service.ExecuteAsync("fail", null));
    }
}
