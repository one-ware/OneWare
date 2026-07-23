using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace OneWare.Terminal.Provider.Win32;

public class Win32ConPtyPseudoTerminal : IPseudoTerminal
{
    private readonly IntPtr _pseudoConsole;
    private readonly FileStream _stdin;
    private readonly FileStream _stdout;
    private readonly FileStream _controlInput;
    private readonly FileStream _controlOutput;
    private bool _isDisposed;

    public Win32ConPtyPseudoTerminal(Process process, IntPtr pseudoConsole, SafeFileHandle inputWrite,
        SafeFileHandle outputRead, SafeFileHandle controlRead, SafeFileHandle acknowledgementWrite)
    {
        Process = process;
        _pseudoConsole = pseudoConsole;
        _stdin = new FileStream(inputWrite, FileAccess.Write, 4096, false);
        _stdout = new FileStream(outputRead, FileAccess.Read, 4096, false);
        _controlInput = new FileStream(controlRead, FileAccess.Read, 4096, true);
        _controlOutput = new FileStream(acknowledgementWrite, FileAccess.Write, 4096, true);
    }

    public Process Process { get; }

    public void SetSize(int columns, int rows)
    {
        if (_pseudoConsole != IntPtr.Zero && columns >= 1 && rows >= 1)
            ConPtyNative.ResizePseudoConsole(_pseudoConsole, new ConPtyNative.Coord((short)columns, (short)rows));
    }

    public async Task WriteAsync(byte[] buffer, int offset, int count)
    {
        if (buffer.Length == 1 && buffer[0] == 10)
            buffer[0] = 13;

        await _stdin.WriteAsync(buffer, offset, count).ConfigureAwait(false);
        await _stdin.FlushAsync().ConfigureAwait(false);
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        return await _stdout.ReadAsync(buffer, offset, count).ConfigureAwait(false);
    }

    public Task<int> ReadControlAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _controlInput.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public async Task WriteControlAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken)
    {
        await _controlOutput.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        await _controlOutput.FlushAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _stdin.Dispose();
        _stdout.Dispose();
        _controlInput.Dispose();
        _controlOutput.Dispose();

        if (_pseudoConsole != IntPtr.Zero)
            ConPtyNative.ClosePseudoConsole(_pseudoConsole);
    }
}