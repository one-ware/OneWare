﻿using System.Diagnostics;
using static winpty.WinPty;

namespace OneWare.Terminal.Provider.Win32;

public class Win32PseudoTerminal : IPseudoTerminal
{
    private readonly IntPtr _cfg = IntPtr.Zero;
    private IntPtr _err = IntPtr.Zero;
    private readonly IntPtr _handle = IntPtr.Zero;
    private bool _isDisposed;
    private readonly IntPtr _spawnCfg = IntPtr.Zero;
    private readonly Stream _stdin;
    private readonly Stream _stdout;

    public Win32PseudoTerminal(Process process, IntPtr handle, IntPtr cfg, IntPtr spawnCfg, IntPtr err, Stream stdin,
        Stream stdout)
    {
        Process = process;

        _handle = handle;
        _stdin = stdin;
        _stdout = stdout;

        _cfg = cfg;
        _spawnCfg = spawnCfg;
        _err = err;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _stdin?.Dispose();
            _stdout?.Dispose();
            winpty_config_free(_cfg);
            winpty_spawn_config_free(_spawnCfg);
            winpty_error_free(_err);
            winpty_free(_handle);
        }
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        return await _stdout.ReadAsync(buffer, offset, count);
    }

    public async Task WriteAsync(byte[] buffer, int offset, int count)
    {
        if (buffer.Length == 1 && buffer[0] == 10) buffer[0] = 13;

        await _stdin.WriteAsync(buffer, offset, count);
    }

    public void SetSize(int columns, int rows)
    {
        if (_cfg != IntPtr.Zero && columns >= 1 && rows >= 1) winpty_set_size(_handle, columns, rows, out _err);
    }

    public Process Process { get; }
}