using System;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace OneWare.Terminal.Provider.Win32;

public class Win32ConPtyPseudoTerminal : IPseudoTerminal
{
    private readonly IntPtr _pseudoConsole;
    private readonly FileStream _stdin;
    private readonly FileStream _stdout;
    private bool _isDisposed;

    public Win32ConPtyPseudoTerminal(Process process, IntPtr pseudoConsole, SafeFileHandle inputWrite,
        SafeFileHandle outputRead)
    {
        Process = process;
        _pseudoConsole = pseudoConsole;
        _stdin = new FileStream(inputWrite, FileAccess.Write, 4096, true);
        _stdout = new FileStream(outputRead, FileAccess.Read, 4096, true);
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

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _stdin.Dispose();
        _stdout.Dispose();

        if (_pseudoConsole != IntPtr.Zero)
            ConPtyNative.ClosePseudoConsole(_pseudoConsole);
    }
}
