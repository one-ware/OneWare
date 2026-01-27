using System.ComponentModel;
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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var namePtr = Native.ptsname(_cfg);
                    var name = Marshal.PtrToStringAnsi(namePtr);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"stty rows {rows} cols {columns} < {name}\"",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    };

                    using var process = Process.Start(psi);
                    process?.WaitForExit();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });
        }
        else
        {
            // TODO Find out why this method doesnt work on macos

            var size = new Native.winsize
            {
                ws_row = (ushort)(rows > 0 ? rows : 24),
                ws_col = (ushort)(columns > 0 ? columns : 80)
            };

            if (Native.ioctl(_cfg, Native.TIOCSWINSZ, ref size) == -1)
            {
                var errno = Marshal.GetLastWin32Error();
                var errorMessage = new Win32Exception(errno).Message;
                throw new Exception($"Failed to resize terminal: {errorMessage} (errno: {errno})");
            }
        }
    }
}