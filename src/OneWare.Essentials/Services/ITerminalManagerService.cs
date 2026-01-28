using Dock.Model.Core;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface ITerminalManagerService : IDockable
{
    Task<TerminalExecutionResult> ExecuteInTerminalAsync(string command,
        string id, string? workingDirectory = null, bool showInUi = false, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}