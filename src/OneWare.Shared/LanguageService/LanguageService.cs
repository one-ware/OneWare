using System.Diagnostics;
using System.Net.WebSockets;
using Nerdbank.Streams;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Shared.LanguageService
{
    public abstract class LanguageService : LanguageServiceBase, ILanguageService
    {
        private CancellationTokenSource? _cancellation;
        private Process? _process;
        private readonly Uri? _webUrl;
        private readonly string? _executablePath;
        private readonly string? _arguments;

        protected LanguageService(string name, Uri webUrl, string? workspace) : base (name, workspace)
        {
            _webUrl = webUrl;
        }

        protected LanguageService(string name, string executablePath, string? arguments, string? workspace) : base (name, workspace)
        {
            _executablePath = executablePath;
            _arguments = arguments;
        }

        public abstract ITypeAssistance GetTypeAssistance(IEditor editor);
        
        public override async Task ActivateAsync()
        {
            if (IsActivated) return;
            IsActivated = true;
            
            if (_webUrl != null)
            {
                var websocket = new ClientWebSocket();
                var websocketUri = new Uri("");
                try
                {
                    _cancellation = new CancellationTokenSource();
                    await websocket.ConnectAsync(websocketUri, _cancellation.Token);
                    
                    await InitAsync(websocket.UsePipeReader().AsStream(), websocket.UsePipeWriter().AsStream());
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }
            }
            else if(_executablePath != null)
            {
                if (!Tools.Exists(_executablePath))
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error($"{Name} language server not found! {_executablePath}");
                    return;
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _executablePath,
                    Arguments = _arguments ?? "",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false
                };

                _process = new Process
                {
                    StartInfo = processStartInfo
                };

                try
                {
                    _process.Start();

                    await InitAsync(_process.StandardInput.BaseStream, _process.StandardOutput.BaseStream);
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }
            }
        }

        public override async Task DeactivateAsync()
        {
            await base.DeactivateAsync();
            _cancellation?.Cancel();
            _process?.Kill();
        }
    }
}