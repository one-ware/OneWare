using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OneWare.Terminal.Provider.Unix;

public class UnixPseudoTerminal : IPseudoTerminal
{
    private readonly int _cfg;
    private readonly Stream _stdin;
    private readonly Stream _stdout;
    private bool _isDisposed;

    public UnixPseudoTerminal(Process process, int cfg, Stream stdin, Stream stdout)
    {
        Process = process;
        _stdin = stdin;
        _stdout = stdout;
        _cfg = cfg;
    }

    public Process Process { get; }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _stdin.Dispose();
        _stdout.Dispose();
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        return await _stdout.ReadAsync(buffer, offset, count);
    }

    public async Task WriteAsync(byte[] buffer, int offset, int count)
    {
        if (buffer is [10]) buffer[0] = 13;

        await Task.Run(() =>
        {
            var buf = Marshal.AllocHGlobal(count);
            Marshal.Copy(buffer, offset, buf, count);
            Native.write(_cfg, buf, count);

            Marshal.FreeHGlobal(buf);
        });
    }

    public void SetSize(int columns, int rows)
    {
        var size = new Native.winsize
        {
            ws_row = (ushort)(rows > 0 ? rows : 24),
            ws_col = (ushort)(columns > 0 ? columns : 80)
        };
        
        try
        {
            var ptr = Native.StructToPtr(size);
            Native.ioctl(_cfg, Native.TIOCSWINSZ, ptr);
            Marshal.FreeHGlobal(ptr);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}