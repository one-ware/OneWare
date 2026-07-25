using System.Diagnostics;

namespace OneWare.Terminal.Provider;

public interface IPseudoTerminal : IDisposable
{
    Process Process { get; }
    void SetSize(int columns, int rows);

    Task WriteAsync(byte[] buffer, int offset, int count);

    Task<int> ReadAsync(byte[] buffer, int offset, int count);

    /// <summary>
    /// Returns the exit code of the terminal's child process once it has exited,
    /// or <see langword="null"/> if it is still running or the code cannot be determined.
    /// May block briefly while the process finishes terminating.
    /// </summary>
    int? GetExitCode();
}