using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class IpcCommandService(ILogger logger) : IIpcCommandService
{
    private readonly ConcurrentDictionary<string, Func<string?, Task>> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public void RegisterCommand(string name, Func<string?, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);

        _handlers[name] = handler;
    }

    public async Task<bool> ExecuteAsync(string name, string? argument)
    {
        if (!_handlers.TryGetValue(name, out var handler))
        {
            logger.LogWarning("Received unknown IPC command '{Command}'.", name);
            return false;
        }

        try
        {
            await handler(argument);
        }
        catch (Exception e)
        {
            logger.LogError(e, "IPC command '{Command}' failed.", name);
        }

        return true;
    }
}
