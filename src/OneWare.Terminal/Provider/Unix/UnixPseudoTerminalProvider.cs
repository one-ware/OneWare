using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace OneWare.Terminal.Provider.Unix;

public class UnixPseudoTerminalProvider : IPseudoTerminalProvider
{
    public IPseudoTerminal? Create(int columns, int rows, string initialDirectory, string command, string? environment,
        string? arguments)
    {
        var winsize = new Native.winsize
        {
            ws_row = (ushort)rows,
            ws_col = (ushort)columns,
            ws_xpixel = 0,
            ws_ypixel = 0
        };

        //Collect ENV Vars before fork to avoid EntryPointNotFoundException
        var envMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var env = Environment.GetEnvironmentVariables();
        foreach (var variable in env.Keys)
        {
            var key = variable?.ToString();
            if (string.IsNullOrWhiteSpace(key) || key is "TERM" or "VTE_VERSION") continue;
            if (env[variable] is string value) envMap[key] = value;
        }

        if (!string.IsNullOrWhiteSpace(environment))
        {
            foreach (var entry in environment.Split('\0'))
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                var separatorIndex = entry.IndexOf('=');
                if (separatorIndex <= 0) continue;
                var key = entry.Substring(0, separatorIndex);
                var value = entry.Substring(separatorIndex + 1);
                envMap[key] = value;
            }
        }

        var envVars = new List<string>(envMap.Count + 2);
        foreach (var pair in envMap)
            envVars.Add($"{pair.Key}={pair.Value}");

        envVars.Add("TERM=xterm-256color");
        envVars.Add(null!);

        // Build all managed data (argv/env arrays) in the PARENT before forking.
        // After forkpty the child shares the address space of a multithreaded runtime whose GC
        // and JIT locks may be held by other (now-frozen) threads. Running not-yet-JIT-compiled
        // managed code or allocating there can deadlock or segfault (exit 139), so the child must
        // only perform native calls on pre-built arrays.
        var envArray = envVars.ToArray();

        var argvList = new List<string> { command };
        if (arguments != null) argvList.AddRange(arguments.Split(' '));
        argvList.Add(null!);
        var argvArray = argvList.ToArray();

        var pid = Native.forkpty(out var masterFd, IntPtr.Zero, IntPtr.Zero, ref winsize);

        //pid will be 0 on the forked process
        if (pid == 0)
        {
            Native.chdir(initialDirectory);
            Native.execve(argvArray[0], argvArray, envArray);
            Native._exit(1);
        }

        var stdin = Native.dup(masterFd);
        var process = Process.GetProcessById(pid);

        return new UnixPseudoTerminal(process, masterFd, new FileStream(new SafeFileHandle(new IntPtr(stdin), true),
            FileAccess.Write), new FileStream(new SafeFileHandle(new IntPtr(masterFd), true), FileAccess.Read));
    }
}
