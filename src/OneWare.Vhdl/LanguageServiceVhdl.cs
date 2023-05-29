using System.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Models;
using Prism.Ioc;

namespace OneWare.Vhdl
{
    public class LanguageServiceVhdl : LanguageServiceBase
    {
        public override bool WorkspaceDependent => true;
        public override string Name => "VHDL LS";

        public LanguageServiceVhdl(string workSpace) : base (workSpace, new []{".vhd", ".vhdl"}, "VHDLLS")
        {
            // Global.Options.WhenAnyValue(x => x.VhdlLspActivated).Subscribe(x =>
            // {
            //     //Check if file is open
            //     var anyFile = MainDock.OpenDocuments.Any(
            //         keyValuePair => SupportedFileTypes.Contains(keyValuePair.Key.Type) && Path.GetFullPath(keyValuePair.Key.FullPath).StartsWith(Path.GetFullPath(workSpace)));
            //     if (anyFile && x && !IsActivated) _ = ActivateAsync();
            //     else if (!x && IsActivated) _ = DeactivateAsync();
            // });
        }

        public override async Task ActivateAsync()
        {
            if (IsActivated) return;
            IsActivated = true;

            if (!Tools.Exists(Global.Options.VhdlLspPath))
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("VHDL language server not found!", null, false);
                return;
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = Global.Options.VhdlLspPath,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false
            };

            var process = new Process
            {
                StartInfo = processStartInfo
            };

            try
            {
                process.Start();

                await InitAsync(process);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
        }

        public override IEnumerable<ErrorListItemModel> ConvertErrors(PublishDiagnosticsParams pdp, ProjectFile file)
        {
            if (file.IsValid() && file.HasRoot &&
                file.TopFolder.Search(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip", false) != null)
                return new List<ErrorListItemModel>();

            return base.ConvertErrors(pdp, file);
        }
    }
}