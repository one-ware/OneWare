using Dock.Model.Core;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface ITerminalManagerService : IDockable
{
    /// <summary>
    /// Executes a command in a terminal tab and returns the result.
    /// </summary>
    Task<TerminalExecutionResult> ExecuteInTerminalAsync(string command,
        string id, string? workingDirectory = null, bool showInUi = false, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
