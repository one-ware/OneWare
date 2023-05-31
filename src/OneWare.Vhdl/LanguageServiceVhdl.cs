using System.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Ioc;
using IFile = OneWare.Shared.IFile;

namespace OneWare.Vhdl
{
    public class LanguageServiceVhdl : LanguageServiceBase, ILanguageService
    {
        public LanguageServiceVhdl(string workspace) : base ("VHDL LS", workspace)
        {
            // Global.Options.WhenAnyValue(x => x.VhdlLspActivated).Subscribe(x =>
            // {
            //     //Check if file is open
            //     var anyFile = MainDock.OpenDocuments.Any(
            //         keyValuePair => SupportedFileTypes.Contains(keyValuePair.Key.Type) && Path.GetFullPath(keyValuePair.Key.FullPath).StartsWith(Path.GetFullPath(workSpace)));
            //     if (anyFile && x && !IsActivated) _ = ActivateAsync();
            //     else if (!x && IsActivated) _ = DeactivateAsync();
            // });
            //TextMateLanguage = new Language()
        }
        
        public ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new TypeAssistanceVhdl(editor, this);
        }

        public override async Task ActivateAsync()
        {
            if (IsActivated) return;
            IsActivated = true;

            var vhdlPath = @"C:\Users\Hendrik\VHDPlus\Packages\rusthdl\vhdl_ls-x86_64-pc-windows-msvc\bin\vhdl_ls.exe";

            // if (!Tools.Exists(Global.Options.VhdlLspPath))
            // {
            //     ContainerLocator.Container.Resolve<ILogger>()?.Error("VHDL language server not found!", null, false);
            //     return;
            // }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = vhdlPath,
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

        public override IEnumerable<ErrorListItemModel> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
        {
            if (file is IProjectFile pf && pf.TopFolder?.Search(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip", false) != null)
                return new List<ErrorListItemModel>();

            return base.ConvertErrors(pdp, file);
        }
    }
}