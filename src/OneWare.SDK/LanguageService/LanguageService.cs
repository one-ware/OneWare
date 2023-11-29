using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using Asmichi.ProcessManagement;
using Nerdbank.Streams;
using OneWare.SDK.Helpers;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;
using Prism.Ioc;

namespace OneWare.SDK.LanguageService
{
    public abstract class LanguageService : LanguageServiceBase, ILanguageService
    {
        private CancellationTokenSource? _cancellation;
        private IChildProcess? _process;
        protected string? Arguments { get; set; }
        protected string? ExecutablePath { get; set; }

        protected LanguageService(string name, string? executablePath, string? arguments, string? workspace) : base(name,
            workspace)
        {
            if (executablePath != null && (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
            {
                PlatformHelper.ChmodFile(executablePath);
            }
            ExecutablePath = executablePath;
            Arguments = arguments;
        }

        public override async Task ActivateAsync()
        {
            //return;
            if (IsActivated) return;

            if (ExecutablePath == null)
            {
                ContainerLocator.Container.Resolve<ILogger>().Warning($"Tried to activate Language Server {Name} without executable!", new NotSupportedException(), false);
                return;
            }
            
            _cancellation = new CancellationTokenSource();
            
            if (ExecutablePath.StartsWith("wss://") || ExecutablePath.StartsWith("ws://"))
            {
                var websocket = new ClientWebSocket();
                try
                {
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
                if (!PlatformHelper.Exists(ExecutablePath))
                {
                    ContainerLocator.Container.Resolve<ILogger>()
                        ?.Warning($"{Name} language server not found! {ExecutablePath}");
                    return;
                }
                
                var argumentArray = Arguments != null ? 
                    Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries).ToArray() : Array.Empty<string>();
                
                var processStartInfo = new ChildProcessStartInfo(ExecutablePath, argumentArray)
                {
                    StdOutputRedirection = OutputRedirection.OutputPipe,
                    StdInputRedirection = InputRedirection.InputPipe,
                    StdErrorRedirection = OutputRedirection.ErrorPipe,
                };
                
                //_process.ErrorDataReceived +=
                //    (o, i) => ContainerLocator.Container.Resolve<ILogger>()?.Error(i.Data ?? "");

                try
                {
                    _process = ChildProcess.Start(processStartInfo);

                    var reader = new StreamReader(_process.StandardError);
                    _ = Task.Run(() =>
                    {
                        while (_process.HasStandardError)
                        {
                            Console.WriteLine(reader.ReadToEnd());
                        }
                    }, _cancellation.Token);
                    
                    await InitAsync(_process.StandardOutput, _process.StandardInput);
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