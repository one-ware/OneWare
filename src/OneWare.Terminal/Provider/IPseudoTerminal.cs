using System.Diagnostics;

namespace OneWare.Terminal.Provider;

public interface IPseudoTerminal : IDisposable
{
    Process Process { get; }
    void SetSize(int columns, int rows);

    Task WriteAsync(byte[] buffer, int offset, int count);

    Task<int> ReadAsync(byte[] buffer, int offset, int count);
}