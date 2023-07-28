using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OneWare.Terminal.Provider.Unix
{
    public class UnixPseudoTerminalProvider : IPseudoTerminalProvider
    {
        public IPseudoTerminal? Create(int columns, int rows, string initialDirectory, string? environment, string? command, params string[]? arguments)
        {
            //Create PseudoTerminal
            var fdm = Native.open("/dev/ptmx", Native.O_RDWR | Native.O_NOCTTY);
            Native.grantpt(fdm);
            Native.unlockpt(fdm);

            var namePtr = Native.ptsname(fdm);
            var name = Marshal.PtrToStringAnsi(namePtr);
            if (name == null) throw new NullReferenceException(nameof(name));

            //Collect ENV Vars before to avoid EntryPointNotFoundException
            var envVars = new List<string>();
            var env = Environment.GetEnvironmentVariables();
            foreach (var variable in env.Keys) 
            {
                if (variable.ToString() != "TERM") {
                    envVars.Add($"{variable}={env[variable]}");
                }
            }
            envVars.Add("TERM=xterm-256color");
            envVars.Add(null!);

            //Duplicate current process
            var pid = Native.fork(); 
            
            //Newly created child process will have PID = 0
            if (pid == 0)
            {
                //Open Slave Process with ReadWrite Access
                var slave = Native.open(name, Native.O_RDWR);
                Native.dup2(slave, 0);
                Native.dup2(slave, 1);
                Native.dup2(slave, 2);

                Native.setsid();
                Native.ioctl(slave, Native.TIOCSCTTY, IntPtr.Zero);
                Native.chdir(initialDirectory);
                
                var argsArray = new List<string> { "/bin/bash" };
                if(arguments?.Length > 0 && !string.IsNullOrEmpty(arguments[0])) 
                    argsArray.AddRange(arguments);
                
                argsArray.Add(null!);

                Native.execve(argsArray[0], argsArray.ToArray(), envVars.ToArray());
            }
            
            var stdin = Native.dup(fdm);
            var process = Process.GetProcessById(pid);
            return new UnixPseudoTerminal(process, stdin, new FileStream(new SafeFileHandle(new IntPtr(stdin), true), 
                FileAccess.Write), new FileStream(new SafeFileHandle(new IntPtr(fdm), true), FileAccess.Read));
        }
    }
}
