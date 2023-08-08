using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.Helpers;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
using Prism.Ioc;
using IFile = OneWare.Shared.Models.IFile;

namespace OneWare.Vhdl
{
    public class LanguageServiceVhdl : LanguageService
    {
        private static string? _executablePath;
        static LanguageServiceVhdl()
        {
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            
            _executablePath = PlatformHelper.Platform switch
            {
                PlatformId.LinuxX64 => $"{assemblyPath}/vhdl_ls-x86_64-unknown-linux-musl/bin/vhdl_ls",
                PlatformId.Wasm => "wss://oneware-cloud-ls-vhdl-qtuhvc77rq-ew.a.run.app",
                _ => null
            };
        }
        
        public LanguageServiceVhdl(string workspace) : base ("RustHDL", _executablePath ?? "", null, workspace)
        {
            
        }

        public override ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new TypeAssistanceVhdl(editor, this);
        }

        public override IEnumerable<ErrorListItemModel> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
        {
            if (file is IProjectFile pf && pf.TopFolder?.Search(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip", false) != null)
                return new List<ErrorListItemModel>();

            return base.ConvertErrors(pdp, file);
        }
    }
}