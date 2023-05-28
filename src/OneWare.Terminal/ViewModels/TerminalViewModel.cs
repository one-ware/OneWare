using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;
using AvalonStudio.Terminals;
using AvalonStudio.Terminals.Unix;
using AvalonStudio.Terminals.Win32;
using Dock.Model.Mvvm.Controls;

using OneWare.Shared;
using OneWare.Shared.Services;
using VtNetCore.Avalonia;
using VtNetCore.VirtualTerminal;

namespace OneWare.Terminal.ViewModels
{
    public class TerminalViewModel : Tool
    {
        private static readonly IPsuedoTerminalProvider SProvider = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new Win32PsuedoTerminalProvider()
            : new UnixPsuedoTerminalProvider();

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
        
        private bool _terminalLoading;
        public bool TerminalLoading
        {
            get => _terminalLoading;
            set => SetProperty(ref _terminalLoading, value);
        }

        public event EventHandler? TerminalReady;
        
        public TerminalViewModel(ISettingsService settingsService) //If created manually
        {
            Title = "Terminal";
            Id = "Terminal";
            
            WorkingDir = "C:/";
            StartArguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"powershell.exe -NoExit Set-Location '{WorkingDir}'" : null; ;
            
            settingsService.GetSettingObservable<string>("General_SelectedTheme").Throttle(TimeSpan.FromMilliseconds(5))
                .Subscribe(x => Dispatcher.UIThread.Post(Redraw));
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
            if (Connection is not { IsConnected: true })
                lock (_createLock)
                {
                    CloseConnection();

                    var shellExecutable = PlatformSupport.ResolveFullExecutablePath(
                        (Platform.PlatformIdentifier == Platform.PlatformId.Win32Nt ? "powershell" : "bash") +
                        Platform.ExecutableExtension);

                    if (!string.IsNullOrEmpty(shellExecutable))
                    {
                        var terminal = SProvider.Create(80, 32, WorkingDir, null, shellExecutable, StartArguments);

                        Connection = new PsuedoTerminalConnection(terminal);

                        Terminal = new VirtualTerminalController();

                        TerminalVisible = true;
                        TerminalLoading = true;

                        Connection.Connect();
                        
                        TerminalReady += Terminal_Ready;

                        TerminalReady?.Invoke(this, EventArgs.Empty);
                    }
                }
        }

        public void Terminal_Ready(object? sender, EventArgs e)
        {
            TerminalLoading = false;
        }

        public void Send(string command)
        {
            if(Connection?.IsConnected ?? false) Connection.SendData(Encoding.ASCII.GetBytes($"{command}\r"));
        }

        public void CloseConnection()
        {
            if (Connection != null)
            {
                Connection.Disconnect();
                Connection = null;
            }
        }

        //public override void OnOpen()
        //{
        //    CreateConnection();

        //Observable.FromEventPattern<SolutionChangedEventArgs>(_studio, nameof(_studio.SolutionChanged)).Subscribe(args =>
        //{
        //    CreateConnection();
        //});

        //    base.OnOpen();
        //}

        public override bool OnClose()
        {
            CloseConnection();
            
            return base.OnClose();
        }

        // public static void ExecScriptInTerminal(string scriptPath, bool elevated, string title)
        // {
        //     try
        //     {
        //         Tools.ExecBash("chmod u+x " + scriptPath);
        //
        //         var sudo = elevated ? "sudo " : "";
        //         var terminal = Global.Factory.AddTerminal(title ?? "", "", App.Paths.PackagesDirectory, true);
        //
        //         async void OnTerminalOnTerminalReady(object o, EventArgs i)
        //         {
        //             terminal.TerminalReady -= OnTerminalOnTerminalReady;
        //             await Task.Delay(100);
        //             terminal.Send($"{sudo}{scriptPath}");
        //         }
        //         terminal.TerminalReady += OnTerminalOnTerminalReady;
        //     }
        //     catch (Exception e)
        //     {
        //         ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        //     }
        // }
    }
}