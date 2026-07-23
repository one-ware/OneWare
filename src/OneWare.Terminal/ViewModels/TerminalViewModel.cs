using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Terminal.Provider;
using OneWare.Terminal.Provider.Unix;
using OneWare.Terminal.Provider.Win32;
using VtNetCore.Avalonia;
using VtNetCore.VirtualTerminal;

namespace OneWare.Terminal.ViewModels;

public class TerminalViewModel : ObservableObject
{
    private static readonly IPseudoTerminalProvider SProvider = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? new Win32ConPtyPseudoTerminalProvider()
        : new UnixPseudoTerminalProvider();

    private readonly Lock _createLock = new();

    public TerminalViewModel(string workingDir, string? startArguments = null)
    {
        WorkingDir = workingDir;
        StartArguments = startArguments ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? BuildWindowsStartArguments(WorkingDir)
            : null);
    }

    public string? StartArguments { get; }
    public string WorkingDir { get; }

    public IConnection? Connection
    {
        get;
        set => SetProperty(ref field, value);
    }


    public VirtualTerminalController? Terminal
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool TerminalVisible
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool TerminalLoading
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public event EventHandler? TerminalReady;

    public void Redraw()
    {
        if (TerminalVisible)
        {
            TerminalVisible = false;
            TerminalVisible = true;
        }
    }

    public void StartCreate()
    {
        Dispatcher.UIThread.Post(CreateConnection);
    }

    public void CreateConnection()
    {
        if (Connection is { IsConnected: true }) return;
        TerminalLoading = true;

        lock (_createLock)
        {
            CloseConnection();
            
            var shellExecutable = PlatformHelper.Platform switch
            {
                PlatformId.WinX64 or PlatformId.WinArm64 => PlatformHelper.GetFullPath("powershell.exe"),
                PlatformId.LinuxX64 or PlatformId.LinuxArm64 => PlatformHelper.GetFullPath("bash"),
                PlatformId.OsxX64 or PlatformId.OsxArm64 => PlatformHelper.GetFullPath("zsh") ??
                                                            PlatformHelper.GetFullPath("bash"),
                _ => null
            };

            if (!string.IsNullOrEmpty(shellExecutable))
            {
                var startArguments = StartArguments;
                if (string.IsNullOrWhiteSpace(startArguments) &&
                    Path.GetFileName(shellExecutable).Equals("zsh", StringComparison.OrdinalIgnoreCase))
                {
                    // Ensure zsh runs interactively so precmd hooks fire.
                    startArguments = "-i";
                }

                var terminal = SProvider.Create(80, 32, WorkingDir, shellExecutable, null, startArguments);

                if (terminal == null)
                {
                    ContainerLocator.Container.Resolve<ILogger>().Error("Error creating terminal!");
                    return;
                }

                Connection = new PseudoTerminalConnection(terminal);

                Terminal = new VirtualTerminalController();

                _ = Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    TerminalVisible = true;
                    Connection.Connect();

                    await Task.Delay(500);

                    TerminalLoading = false;

                    TerminalReady?.Invoke(this, EventArgs.Empty);
                });
            }
        }
    }

    public void Send(string command)
    {
        if (Connection?.IsConnected ?? false) Connection.SendData(Encoding.ASCII.GetBytes($"{command}\r"));
    }

    public void SendInterrupt()
    {
        // Send Ctrl+C (ETX) to abort the currently running foreground command
        // so the shell returns to a usable prompt.
        if (Connection?.IsConnected ?? false) Connection.SendData([0x03]);
    }

    public void KillProcess()
    {
        // Forcibly terminate the shell and any child processes. Used as a last
        // resort when an interrupt (Ctrl+C) fails to free a stuck command.
        if (Connection is PseudoTerminalConnection ptc) ptc.KillProcess();
    }

    public void SuppressEcho(byte[] data)
    {
        if (Connection is IOutputSuppressor suppressor)
        {
            suppressor.SuppressOutput(data);
        }
    }

    public string BuildCompletionCommand(string executionId)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "$__ow_success=$?; " +
                   "$__ow_exit=if ($__ow_success) { 0 } elseif ($global:LASTEXITCODE -ne 0) " +
                   "{ [int]$global:LASTEXITCODE } else { 1 }; " +
                   "$__ow_esc=[char]27; Write-Host ($__ow_esc + '[1A' + \"`r\" + $__ow_esc + '[2K') -NoNewline; " +
                   $"$global:__ow_completion.WriteLine('{executionId}:' + $__ow_exit); " +
                   "[void]$global:__ow_ack.ReadLine()";
        }

        return
            $"__ow_exit=$?; printf '\\033[1A\\r\\033[2K'; printf '{executionId}:%s\\n' \"$__ow_exit\" >&198; " +
            "IFS= read -r __ow_ack <&199";
    }

    public void CloseConnection()
    {
        if (Connection != null)
        {
            Connection.Disconnect();
            Connection = null;
        }
    }

    public void Close()
    {
        CloseConnection();
    }

    private static string BuildWindowsStartArguments(string workingDir)
    {
        // The arguments string must include the full command line because
        // Win32ConPtyPseudoTerminalProvider.BuildCommandLine returns just the arguments when provided
        var escapedDir = workingDir.Replace("'", "''");

        var bootstrapCmd =
            "$__ow_completion_handle=[Microsoft.Win32.SafeHandles.SafeFileHandle]::new(" +
            "[IntPtr][long]$env:OW_COMPLETION_HANDLE,$false); " +
            "$__ow_completion_stream=[System.IO.FileStream]::new(" +
            "$__ow_completion_handle,[System.IO.FileAccess]::Write,4096,$false); " +
            "$global:__ow_completion=[System.IO.StreamWriter]::new($__ow_completion_stream); " +
            "$global:__ow_completion.AutoFlush=$true; " +
            "$__ow_ack_handle=[Microsoft.Win32.SafeHandles.SafeFileHandle]::new(" +
            "[IntPtr][long]$env:OW_ACK_HANDLE,$false); " +
            "$__ow_ack_stream=[System.IO.FileStream]::new(" +
            "$__ow_ack_handle,[System.IO.FileAccess]::Read,4096,$false); " +
            "$global:__ow_ack=[System.IO.StreamReader]::new($__ow_ack_stream); " +
            $"Set-Location '{escapedDir}'";

        return $"powershell.exe -NoProfile -NoExit -Command \"{bootstrapCmd}\"";
    }
}
