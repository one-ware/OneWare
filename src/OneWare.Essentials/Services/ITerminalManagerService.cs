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

    /// <summary>
    /// Creates a terminal that is hosted by the caller — e.g. the mini terminal inside an AI
    /// chat tool box — instead of being shown as a tab in the terminal pane. Bind the returned
    /// object to a <c>ContentControl</c> to render it.
    /// </summary>
    /// <param name="title">Title used for the tab if the terminal is later moved into the pane.</param>
    /// <param name="command">The command line that <see cref="ExecuteInTerminalAsync(IEmbeddedTerminal, TimeSpan?, IProgress{string}, CancellationToken)"/> will run.</param>
    /// <param name="workingDirectory">Working directory, or null for the active project.</param>
    IEmbeddedTerminal CreateEmbeddedTerminal(string title, string command, string? workingDirectory = null)
        => throw new NotSupportedException("This terminal manager does not support embedded terminals.");

    /// <summary>
    /// Runs the command of an embedded terminal created by
    /// <see cref="CreateEmbeddedTerminal"/>. The shell stays alive afterwards so the user can
    /// keep interacting with it; call <see cref="IEmbeddedTerminal.CloseShell"/> to release it.
    /// </summary>
    Task<TerminalExecutionResult> ExecuteInTerminalAsync(IEmbeddedTerminal terminal, TimeSpan? timeout = null,
        IProgress<string>? outputProgress = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This terminal manager does not support embedded terminals.");
}
