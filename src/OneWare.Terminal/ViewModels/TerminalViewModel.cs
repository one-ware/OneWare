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
        StartArguments = startArguments;
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

    /// <summary>
    /// Raised when the underlying shell exits or the pty closes on its own,
    /// i.e. not through <see cref="Close"/>. Lets owners close the containing tab.
    /// </summary>
    public event EventHandler? ConnectionClosed;

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
                // Shell integration is installed via startup files, so the shell emits
                // invisible OSC 633 command lifecycle sequences without anything being
                // typed into (or echoed by) the terminal.
                var integration = ShellIntegration.GetSpawnConfig(shellExecutable);
                var startArguments = StartArguments ?? integration.Arguments;

                var terminal = SProvider.Create(80, 32, WorkingDir, shellExecutable, integration.Environment,
                    startArguments);

                if (terminal == null)
                {
                    ContainerLocator.Container.Resolve<ILogger>().Error("Error creating terminal!");
                    return;
                }

                Connection = new PseudoTerminalConnection(terminal);
                Connection.Closed += OnConnectionClosed;

                Terminal = new VirtualTerminalController();

                Dispatcher.UIThread.Post(() =>
                {
                    Connection.Connect();

                    TerminalVisible = true;

                    TerminalLoading = false;

                    TerminalReady?.Invoke(this, EventArgs.Empty);
                });
            }
        }
    }

    public void Send(string command)
    {
        if (Connection?.IsConnected ?? false) Connection.SendData(Encoding.UTF8.GetBytes($"{command}\r"));
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

    public void CloseConnection()
    {
        if (Connection != null)
        {
            Connection.Closed -= OnConnectionClosed;
            Connection.Disconnect();
            Connection = null;
        }
    }

    private void OnConnectionClosed(object? sender, EventArgs e)
    {
        if (sender is IConnection connection)
            connection.Closed -= OnConnectionClosed;
        if (!ReferenceEquals(sender, Connection)) return;

        ConnectionClosed?.Invoke(this, EventArgs.Empty);
    }

    public void Close()
    {
        CloseConnection();
    }
}
