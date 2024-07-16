using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Terminal.Provider;
using OneWare.Terminal.Provider.Unix;
using OneWare.Terminal.Provider.Win32;
using Prism.Ioc;
using VtNetCore.Avalonia;
using VtNetCore.VirtualTerminal;

namespace OneWare.Terminal.ViewModels;

public class TerminalViewModel : ObservableObject
{
    private static readonly IPseudoTerminalProvider SProvider = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? new Win32PseudoTerminalProvider()
        : new UnixPseudoTerminalProvider();

    private readonly object _createLock = new();
    
    public string? StartArguments { get; }
    public string WorkingDir { get; }

    private IConnection? _connection;

    public IConnection? Connection
    {
        get => _connection;
        set => SetProperty(ref _connection, value);
    }

    private VirtualTerminalController? _terminal;

    
    public VirtualTerminalController? Terminal
    {
        get => _terminal;
        set => SetProperty(ref _terminal, value);
    }
    
    private bool _terminalVisible;
    
    public bool TerminalVisible
    {
        get => _terminalVisible;
        set => SetProperty(ref _terminalVisible, value);
    }

    private bool _terminalLoading = true;

    public bool TerminalLoading
    {
        get => _terminalLoading;
        set => SetProperty(ref _terminalLoading, value);
    }

    public event EventHandler? TerminalReady;
    
    public TerminalViewModel(string workingDir, string? startArguments = null)
    {
        WorkingDir = workingDir;
        StartArguments = startArguments ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"powershell.exe -NoExit Set-Location '{WorkingDir}'"
            : null);
    }
    
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
            
            //TODO Fix zsh support
            var shellExecutable = PlatformHelper.Platform switch
            {
                PlatformId.WinX64 or PlatformId.WinArm64 => PlatformHelper.GetFullPath("powershell.exe"),
                PlatformId.LinuxX64 or PlatformId.LinuxArm64 => PlatformHelper.GetFullPath("bash"),
                PlatformId.OsxX64 or PlatformId.OsxArm64 => PlatformHelper.GetFullPath("bash") ??
                                                            PlatformHelper.GetFullPath("bash"),
                _ => null
            };

            if (!string.IsNullOrEmpty(shellExecutable))
            {
                var terminal = SProvider.Create(80, 32, WorkingDir, shellExecutable, null, StartArguments);

                if (terminal == null)
                {
                    ContainerLocator.Container.Resolve<ILogger>().Error("Error creating terminal!");
                    return;
                }

                Connection = new PseudoTerminalConnection(terminal);

                Terminal = new VirtualTerminalController();

                _ = Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await Task.Delay(100);

                    Connection.Connect();
                    
                    await Task.Delay(100);
                    
                    TerminalVisible = true;
                    
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
}