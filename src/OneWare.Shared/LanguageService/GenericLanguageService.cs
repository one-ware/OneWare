using System.Diagnostics;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Shared.LanguageService
{
    internal class GenericLanguageService : LanguageServiceBase, ILanguageService
    {
        private readonly string _arguments;
        private readonly string _serverName;
        private readonly string _langSrvPath;

        public override bool WorkspaceDependent => false;
        public override string Name => "Generic LS";

        public ITypeAssistance TypeAssistance => new GenericTypeAssistanceLsp();

        public GenericLanguageService(string name, string executablePath, string arguments, string workspace, params string[] supportedFiles) : base (workspace, supportedFiles, name)
        {
            _serverName = name;
            _langSrvPath = executablePath;
            _arguments = arguments;
        }

        public override async Task ActivateAsync()
        {
            if (IsActivated) return;
            IsActivated = true;

            if (!File.Exists(_langSrvPath) && !Tools.ExistsOnPath(_langSrvPath))
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(_serverName + " language server not found!", null, false);
                return;
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _langSrvPath,
                Arguments = _arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            var process = new Process
            {
                StartInfo = processStartInfo
            };

            process.ErrorDataReceived += (o, i) => ContainerLocator.Container.Resolve<ILogger>()?.Log(_serverName + " " + i.Data, ConsoleColor.Yellow);
            try
            {
                process.Start();
                process.BeginErrorReadLine();
                await InitAsync(process);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
        }
    }
}