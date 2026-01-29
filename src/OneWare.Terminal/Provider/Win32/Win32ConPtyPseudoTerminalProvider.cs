using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OneWare.Terminal.Provider.Win32;

public class Win32ConPtyPseudoTerminalProvider : IPseudoTerminalProvider
{
    public IPseudoTerminal? Create(int columns, int rows, string initialDirectory, string command, string? environment,
        string? arguments)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return null;

        var commandLine = BuildCommandLine(command, arguments);
        if (string.IsNullOrWhiteSpace(commandLine))
            return null;

        SafeFileHandle? inputRead = null;
        SafeFileHandle? inputWrite = null;
        SafeFileHandle? outputRead = null;
        SafeFileHandle? outputWrite = null;
        var pseudoConsole = IntPtr.Zero;
        var attributeList = IntPtr.Zero;
        var environmentBlock = IntPtr.Zero;
        var terminalCreated = false;

        try
        {
            CreatePipePair(out inputRead, out inputWrite);
            CreatePipePair(out outputRead, out outputWrite);

            var result = ConPtyNative.CreatePseudoConsole(
                new ConPtyNative.Coord((short)columns, (short)rows),
                inputRead,
                outputWrite,
                0,
                out pseudoConsole);

            if (result != 0 || pseudoConsole == IntPtr.Zero)
                return null;

            inputRead.Dispose();
            outputWrite.Dispose();

            InitializeAttributeList(pseudoConsole, out attributeList);

            var startupInfo = new ConPtyNative.StartupInfoEx
            {
                StartupInfo = new ConPtyNative.StartupInfo
                {
                    cb = Marshal.SizeOf<ConPtyNative.StartupInfoEx>()
                },
                lpAttributeList = attributeList
            };

            if (!string.IsNullOrWhiteSpace(environment)) environmentBlock = Marshal.StringToHGlobalUni(environment);

            var creationFlags = ConPtyNative.EXTENDED_STARTUPINFO_PRESENT |
                                (environmentBlock != IntPtr.Zero ? ConPtyNative.CREATE_UNICODE_ENVIRONMENT : 0);

            var commandLineBuilder = new StringBuilder(commandLine);

            if (!ConPtyNative.CreateProcessW(null, commandLineBuilder, IntPtr.Zero, IntPtr.Zero, false, creationFlags,
                    environmentBlock, initialDirectory, ref startupInfo, out var processInformation))
            {
                ConPtyNative.ClosePseudoConsole(pseudoConsole);
                return null;
            }

            ConPtyNative.CloseHandle(processInformation.hThread);
            ConPtyNative.CloseHandle(processInformation.hProcess);

            var process = Process.GetProcessById(processInformation.dwProcessId);

            var terminal = new Win32ConPtyPseudoTerminal(process, pseudoConsole, inputWrite, outputRead);
            terminalCreated = true;
            return terminal;
        }
        catch (Win32Exception)
        {
            return null;
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                ConPtyNative.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (environmentBlock != IntPtr.Zero)
                Marshal.FreeHGlobal(environmentBlock);

            if (!terminalCreated && pseudoConsole != IntPtr.Zero)
                ConPtyNative.ClosePseudoConsole(pseudoConsole);

            if (inputRead is { IsInvalid: false }) inputRead.Dispose();
            if (outputWrite is { IsInvalid: false }) outputWrite.Dispose();
            if (!terminalCreated)
            {
                if (inputWrite is { IsInvalid: false }) inputWrite.Dispose();
                if (outputRead is { IsInvalid: false }) outputRead.Dispose();
            }
        }
    }

    private static string BuildCommandLine(string command, string? arguments)
    {
        if (!string.IsNullOrWhiteSpace(arguments))
            return arguments;

        return command;
    }

    private static void CreatePipePair(out SafeFileHandle readPipe, out SafeFileHandle writePipe)
    {
        if (!ConPtyNative.CreatePipe(out readPipe, out writePipe, IntPtr.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        if (!ConPtyNative.SetHandleInformation(readPipe, ConPtyNative.HANDLE_FLAG_INHERIT, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        if (!ConPtyNative.SetHandleInformation(writePipe, ConPtyNative.HANDLE_FLAG_INHERIT, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static void InitializeAttributeList(IntPtr pseudoConsole, out IntPtr attributeList)
    {
        attributeList = IntPtr.Zero;
        var size = IntPtr.Zero;

        if (!ConPtyNative.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 122)
                throw new Win32Exception(error);
        }

        attributeList = Marshal.AllocHGlobal(size);

        if (!ConPtyNative.InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        if (!ConPtyNative.UpdateProcThreadAttribute(attributeList, 0,
                ConPtyNative.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, pseudoConsole, IntPtr.Size,
                IntPtr.Zero, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}