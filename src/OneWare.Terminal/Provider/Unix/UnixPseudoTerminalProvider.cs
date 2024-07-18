using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OneWare.Terminal.Provider.Unix;

public class UnixPseudoTerminalProvider : IPseudoTerminalProvider
{
    public IPseudoTerminal? Create(int columns, int rows, string initialDirectory, string command, string? environment,
        string? arguments)
    {
        if (Native.openpty(out var masterFd, out var slaveFd, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) == -1)
        {
            throw new Exception("Failed to open PTY");
        }

        //Collect ENV Vars before fork to avoid EntryPointNotFoundException
        var envVars = new List<string>();
        var env = Environment.GetEnvironmentVariables();
        foreach (var variable in env.Keys)
            if (variable.ToString() is not ("TERM" or "VTE_VERSION"))
                envVars.Add($"{variable}={env[variable]}");
        
        envVars.Add("TERM=xterm-256color");
        envVars.Add(null!);
        
        //Duplicate current process
        var pid = Native.fork();

        //pid will be 0 on the forked process
        if (pid == 0)
        {
            Native.dup2(slaveFd, 0);
            Native.dup2(slaveFd, 1);
            Native.dup2(slaveFd, 2);

            Native.setsid();
            Native.ioctl(slaveFd, Native.TIOCSCTTY, IntPtr.Zero);
            Native.chdir(initialDirectory);

            var argsArray = new List<string> { command };
            if (arguments != null) argsArray.AddRange(arguments.Split(' '));

            argsArray.Add(null!);

            Native.execve(argsArray[0], argsArray.ToArray(), envVars.ToArray());
        }

        var stdin = Native.dup(masterFd);
        var process = Process.GetProcessById(pid);
        
        return new UnixPseudoTerminal(process, stdin, new FileStream(new SafeFileHandle(new IntPtr(stdin), false),
            FileAccess.Write), new FileStream(new SafeFileHandle(new IntPtr(masterFd), false), FileAccess.Read));
    }
}