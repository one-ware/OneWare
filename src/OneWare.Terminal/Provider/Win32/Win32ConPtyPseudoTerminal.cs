using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Win32.SafeHandles;

namespace OneWare.Terminal.Provider.Win32;

public class Win32ConPtyPseudoTerminal : IPseudoTerminal
{
    private readonly IntPtr _pseudoConsole;
    private readonly FileStream _stdin;
    private readonly FileStream _stdout;
    private readonly NamedPipeServerStream _controlPipe;
    private readonly object _controlPipeLock = new();
    private Task _controlConnected;
    private bool _isDisposed;

    public Win32ConPtyPseudoTerminal(Process process, IntPtr pseudoConsole, SafeFileHandle inputWrite,
        SafeFileHandle outputRead, NamedPipeServerStream controlPipe, Task controlConnected)
    {
        Process = process;
        _pseudoConsole = pseudoConsole;
        _stdin = new FileStream(inputWrite, FileAccess.Write, 4096, false);
        _stdout = new FileStream(outputRead, FileAccess.Read, 4096, false);
        _controlPipe = controlPipe;
        _controlConnected = controlConnected;
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

    public async Task<int> ReadControlAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await _controlConnected.WaitAsync(cancellationToken);
            try
            {
                var bytesRead = await _controlPipe.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
                if (bytesRead > 0) return bytesRead;
            }
            catch (IOException) when (!_controlPipe.IsConnected)
            {
                // The helper disconnected before sending a complete frame.
            }

            PrepareNextControlConnection();
        }
    }

    public async Task WriteControlAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken)
    {
        await _controlConnected.WaitAsync(cancellationToken);
        try
        {
            await _controlPipe.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
            await _controlPipe.FlushAsync(cancellationToken);
        }
        catch (IOException) when (!_controlPipe.IsConnected)
        {
            // The command stopped waiting before its acknowledgement arrived.
        }
        finally
        {
            PrepareNextControlConnection();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _stdin.Dispose();
        _stdout.Dispose();
        lock (_controlPipeLock)
        {
            _controlPipe.Dispose();
        }

        if (_pseudoConsole != IntPtr.Zero)
            ConPtyNative.ClosePseudoConsole(_pseudoConsole);
    }

    private void PrepareNextControlConnection()
    {
        lock (_controlPipeLock)
        {
            if (_isDisposed) return;

            if (_controlPipe.IsConnected)
            {
                try
                {
                    _controlPipe.Disconnect();
                }
                catch (IOException)
                {
                    // The client disconnected between the state check and reset.
                }
            }

            _controlConnected = _controlPipe.WaitForConnectionAsync();
        }
    }
}