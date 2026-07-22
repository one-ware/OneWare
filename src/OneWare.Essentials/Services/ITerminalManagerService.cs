using Dock.Model.Core;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface ITerminalManagerService : IDockable
{
    /// <summary>
    /// Executes a command in a terminal tab and returns the result.
    /// </summary>
    /// <param name="outputProgress">
    /// Optional sink that receives the accumulated terminal output as it streams in, enabling
    /// real-time display while the command is still running.
    /// </param>
    Task<TerminalExecutionResult> ExecuteInTerminalAsync(string command,
        string id, string? workingDirectory = null, bool showInUi = false, TimeSpan? timeout = null,
        IProgress<string>? outputProgress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a command in a terminal tab and returns the result.
    /// </summary>
    [Obsolete("Use the overload that accepts an IProgress<string> outputProgress parameter. " +
              "This overload is kept for plugin binary compatibility and will be removed in a future release.")]
    Task<TerminalExecutionResult> ExecuteInTerminalAsync(string command,
        string id, string? workingDirectory, bool showInUi, TimeSpan? timeout,
        CancellationToken cancellationToken);
}
