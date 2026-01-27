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
        var envVars = new List<string>();
        var env = Environment.GetEnvironmentVariables();
        foreach (var variable in env.Keys)
            if (variable.ToString() is not ("TERM" or "VTE_VERSION"))
                envVars.Add($"{variable}={env[variable]}");

        envVars.Add("TERM=xterm-256color");
        envVars.Add(null!);

        var pid = Native.forkpty(out var masterFd, IntPtr.Zero, IntPtr.Zero, ref winsize);

        //pid will be 0 on the forked process
        if (pid == 0)
        {
            Native.chdir(initialDirectory);

            var argsArray = new List<string> { command };
            if (arguments != null) argsArray.AddRange(arguments.Split(' '));

            argsArray.Add(null!);

            Native.execve(argsArray[0], argsArray.ToArray(), envVars.ToArray());
            Environment.Exit(1);
        }

        var stdin = Native.dup(masterFd);
        var process = Process.GetProcessById(pid);

        return new UnixPseudoTerminal(process, masterFd, new FileStream(new SafeFileHandle(new IntPtr(stdin), true),
            FileAccess.Write), new FileStream(new SafeFileHandle(new IntPtr(masterFd), true), FileAccess.Read));
    }
}