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
        private readonly string? _arguments;
        protected readonly string ExecutablePath;

        protected LanguageService(string name, string executablePath, string? arguments, string? workspace) : base(name,
            workspace)
        {
            ExecutablePath = executablePath;
            _arguments = arguments;
        }

        public abstract ITypeAssistance GetTypeAssistance(IEditor editor);

        public override async Task ActivateAsync()
        {
            //return;
            if (IsActivated) return;

            if (ExecutablePath.StartsWith("wss://") || ExecutablePath.StartsWith("ws://"))
            {
                var websocket = new ClientWebSocket();
                try
                {
                    _cancellation = new CancellationTokenSource();
                    await websocket.ConnectAsync(new Uri(ExecutablePath), _cancellation.Token);

                    await InitAsync(websocket.UsePipeReader().AsStream(), websocket.UsePipeWriter().AsStream());
                    IsActivated = true;
                    return;
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                    return;
                }
            }
            else
            {
                if (!Tools.Exists(ExecutablePath))
                {
                    ContainerLocator.Container.Resolve<ILogger>()
                        ?.Error($"{Name} language server not found! {ExecutablePath}");
                    return;
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ExecutablePath,
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

                _process.ErrorDataReceived +=
                    (o, i) => ContainerLocator.Container.Resolve<ILogger>()?.Error(i.Data ?? "");

                try
                {
                    _process.Start();
                    _process.BeginErrorReadLine();

                    await InitAsync(_process.StandardOutput.BaseStream, _process.StandardInput.BaseStream);
                    IsActivated = true;
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