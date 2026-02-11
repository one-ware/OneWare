using System.Diagnostics;
using Asmichi.ProcessManagement;
using OneWare.Essentials.Enums;

namespace OneWare.Essentials.Services;

public interface IChildProcessService
{
    /// <summary>
    /// Starts a child process with the given start info.
    /// </summary>
    public IChildProcess StartChildProcess(ChildProcessStartInfo startInfo);

    /// <summary>
    /// Returns child processes started for a given path.
    /// </summary>
    public IEnumerable<IChildProcess> GetChildProcesses(string path);

    /// <summary>
    /// Terminates the provided child processes.
    /// </summary>
    public void Kill(params IChildProcess[] childProcesses);

    /// <summary>
    /// Executes a tool with status reporting and captures output.
    /// </summary>
    public Task<(bool success, string output)> ExecuteShellAsync(string path, IReadOnlyCollection<string> arguments,
        string workingDirectory,
        string status, AppState state = AppState.Loading, bool showTimer = false,
        Func<string, bool>? outputAction = null,
        Func<string, bool>? errorAction = null);

    /// <summary>
    /// Starts a process and returns a weak reference for tracking.
    /// </summary>
    public WeakReference<Process> StartWeakProcess(string path, IReadOnlyCollection<string> arguments,
        string workingDirectory);
}
