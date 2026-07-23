using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
        SafeFileHandle? completionRead = null;
        SafeFileHandle? completionWrite = null;
        SafeFileHandle? acknowledgementRead = null;
        SafeFileHandle? acknowledgementWrite = null;
        var pseudoConsole = IntPtr.Zero;
        var attributeList = IntPtr.Zero;
        var inheritedHandleList = IntPtr.Zero;
        var environmentBlock = IntPtr.Zero;
        var terminalCreated = false;

        try
        {
            CreatePipePair(out inputRead, out inputWrite);
            CreatePipePair(out outputRead, out outputWrite);
            CreatePipePair(out completionRead, out completionWrite, inheritWrite: true);
            CreatePipePair(out acknowledgementRead, out acknowledgementWrite, inheritRead: true);

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

            InitializeAttributeList(pseudoConsole, completionWrite, acknowledgementRead, out attributeList,
                out inheritedHandleList);

            var startupInfo = new ConPtyNative.StartupInfoEx
            {
                StartupInfo = new ConPtyNative.StartupInfo
                {
                    cb = Marshal.SizeOf<ConPtyNative.StartupInfoEx>()
                },
                lpAttributeList = attributeList
            };

            environment = AddControlEnvironment(environment, completionWrite.DangerousGetHandle(),
                acknowledgementRead.DangerousGetHandle());
            environmentBlock = Marshal.StringToHGlobalUni(environment);

            var creationFlags = ConPtyNative.EXTENDED_STARTUPINFO_PRESENT |
                                ConPtyNative.CREATE_UNICODE_ENVIRONMENT;

            var commandLineBuilder = new StringBuilder(commandLine);

            if (!ConPtyNative.CreateProcessW(null, commandLineBuilder, IntPtr.Zero, IntPtr.Zero, true, creationFlags,
                    environmentBlock, initialDirectory, ref startupInfo, out var processInformation))
            {
                ConPtyNative.ClosePseudoConsole(pseudoConsole);
                return null;
            }

            ConPtyNative.CloseHandle(processInformation.hThread);
            ConPtyNative.CloseHandle(processInformation.hProcess);

            var process = Process.GetProcessById(processInformation.dwProcessId);

            completionWrite.Dispose();
            acknowledgementRead.Dispose();

            var terminal = new Win32ConPtyPseudoTerminal(process, pseudoConsole, inputWrite, outputRead,
                completionRead, acknowledgementWrite);
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

            if (inheritedHandleList != IntPtr.Zero)
                Marshal.FreeHGlobal(inheritedHandleList);

            if (environmentBlock != IntPtr.Zero)
                Marshal.FreeHGlobal(environmentBlock);

            if (!terminalCreated && pseudoConsole != IntPtr.Zero)
                ConPtyNative.ClosePseudoConsole(pseudoConsole);

            if (inputRead is { IsInvalid: false }) inputRead.Dispose();
            if (outputWrite is { IsInvalid: false }) outputWrite.Dispose();
            if (completionWrite is { IsInvalid: false }) completionWrite.Dispose();
            if (acknowledgementRead is { IsInvalid: false }) acknowledgementRead.Dispose();
            if (!terminalCreated)
            {
                if (inputWrite is { IsInvalid: false }) inputWrite.Dispose();
                if (outputRead is { IsInvalid: false }) outputRead.Dispose();
                if (completionRead is { IsInvalid: false }) completionRead.Dispose();
                if (acknowledgementWrite is { IsInvalid: false }) acknowledgementWrite.Dispose();
            }
        }
    }

    private static string BuildCommandLine(string command, string? arguments)
    {
        if (!string.IsNullOrWhiteSpace(arguments))
            return arguments;

        return command;
    }

    private static void CreatePipePair(out SafeFileHandle readPipe, out SafeFileHandle writePipe,
        bool inheritRead = false, bool inheritWrite = false)
    {
        var securityAttributes = new ConPtyNative.SecurityAttributes
        {
            nLength = Marshal.SizeOf<ConPtyNative.SecurityAttributes>(),
            bInheritHandle = true
        };
        var securityAttributesPointer = Marshal.AllocHGlobal(securityAttributes.nLength);
        try
        {
            Marshal.StructureToPtr(securityAttributes, securityAttributesPointer, false);
            if (!ConPtyNative.CreatePipe(out readPipe, out writePipe, securityAttributesPointer, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.FreeHGlobal(securityAttributesPointer);
        }

        if (!inheritRead &&
            !ConPtyNative.SetHandleInformation(readPipe, ConPtyNative.HANDLE_FLAG_INHERIT, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        if (!inheritWrite &&
            !ConPtyNative.SetHandleInformation(writePipe, ConPtyNative.HANDLE_FLAG_INHERIT, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static string AddControlEnvironment(string? environment, IntPtr completionHandle,
        IntPtr acknowledgementHandle)
    {
        var values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(environment))
        {
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && entry.Value is string value)
                    values[key] = value;
            }
        }
        else
        {
            foreach (var entry in environment.Split('\0'))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                var separator = entry.IndexOf('=');
                if (separator > 0)
                    values[entry[..separator]] = entry[(separator + 1)..];
            }
        }

        values["OW_COMPLETION_HANDLE"] = completionHandle.ToInt64().ToString(CultureInfo.InvariantCulture);
        values["OW_ACK_HANDLE"] = acknowledgementHandle.ToInt64().ToString(CultureInfo.InvariantCulture);

        var builder = new StringBuilder();
        foreach (var pair in values)
            builder.Append(pair.Key).Append('=').Append(pair.Value).Append('\0');
        builder.Append('\0');
        return builder.ToString();
    }

    private static void InitializeAttributeList(IntPtr pseudoConsole, SafeFileHandle completionWrite,
        SafeFileHandle acknowledgementRead, out IntPtr attributeList, out IntPtr inheritedHandleList)
    {
        attributeList = IntPtr.Zero;
        inheritedHandleList = IntPtr.Zero;
        var size = IntPtr.Zero;

        if (!ConPtyNative.InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 122)
                throw new Win32Exception(error);
        }

        attributeList = Marshal.AllocHGlobal(size);

        if (!ConPtyNative.InitializeProcThreadAttributeList(attributeList, 2, 0, ref size))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        if (!ConPtyNative.UpdateProcThreadAttribute(attributeList, 0,
                ConPtyNative.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, pseudoConsole, IntPtr.Size,
                IntPtr.Zero, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        inheritedHandleList = Marshal.AllocHGlobal(IntPtr.Size * 2);
        Marshal.WriteIntPtr(inheritedHandleList, 0, completionWrite.DangerousGetHandle());
        Marshal.WriteIntPtr(inheritedHandleList, IntPtr.Size, acknowledgementRead.DangerousGetHandle());

        if (!ConPtyNative.UpdateProcThreadAttribute(attributeList, 0,
                ConPtyNative.PROC_THREAD_ATTRIBUTE_HANDLE_LIST, inheritedHandleList, IntPtr.Size * 2,
                IntPtr.Zero, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}