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
    private int? _exitCode;

    public UnixPseudoTerminal(Process process, int cfg, Stream stdin, Stream stdout)
    {
        Process = process;
        _stdin = stdin;
        _stdout = stdout;
        _cfg = cfg;
    }

    public Process Process { get; }

    public int? GetExitCode()
    {
        // waitpid reaps the child exactly once; cache the result for later callers.
        if (_exitCode != null) return _exitCode;

        try
        {
            const int WNOHANG = 1;
            var status = 0;

            // Called right after pty EOF, so the child is dead or exiting. Poll briefly
            // instead of blocking forever in case the EOF had another cause.
            for (var i = 0; i < 100; i++)
            {
                var result = Native.waitpid(Process.Id, ref status, WNOHANG);
                if (result == Process.Id)
                {
                    if ((status & 0x7f) == 0)
                        _exitCode = (status >> 8) & 0xff; // WIFEXITED → WEXITSTATUS
                    else
                        _exitCode = 128 + (status & 0x7f); // killed by signal, shell convention
                    return _exitCode;
                }

                if (result < 0) return null; // not our child / already reaped elsewhere
                Thread.Sleep(10);
            }
        }
        catch
        {
            // Best effort only.
        }

        return null;
    }

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
            try
            {
                Marshal.Copy(buffer, offset, buf, count);

                // POSIX write() may perform a partial write or return -1 on EINTR/EAGAIN
                // when the pty input buffer is momentarily full. Ignoring that dropped
                // part of the command (often the trailing carriage return), leaving the
                // shell waiting for input and the command hanging forever. Loop until
                // every byte is written so command submission is reliable.
                var written = 0;
                var retries = 0;
                while (written < count && !_isDisposed)
                {
                    var result = Native.write(_cfg, IntPtr.Add(buf, written), count - written);

                    if (result > 0)
                    {
                        written += result;
                        retries = 0;
                        continue;
                    }

                    if (++retries > 1000) break;
                    Thread.Sleep(1);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
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