using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.SDK;
using OneWare.SDK.Helpers;
using OneWare.SDK.LanguageService;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;
using Prism.Ioc;
using IFile = OneWare.SDK.Models.IFile;

namespace OneWare.Vhdl
{
    public class LanguageServiceVhdl : LanguageService
    {
        private static readonly string? StartPath;
        
        static LanguageServiceVhdl()
        {
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            
            StartPath = PlatformHelper.Platform switch
            {
                PlatformId.WinX64 => $"{assemblyPath}/native_tools/win-x64/rusthdl/vhdl_ls-x86_64-pc-windows-msvc/bin/vhdl_ls.exe",
                PlatformId.LinuxX64 => $"{assemblyPath}/native_tools/linux-x64/rusthdl/vhdl_ls-x86_64-unknown-linux-musl/bin/vhdl_ls",
                //PlatformId.Wasm => "wss://oneware-cloud-ls-vhdl-qtuhvc77rq-ew.a.run.app",
                _ => null
            };
        }
        
        public LanguageServiceVhdl(string workspace) : base ("RustHDL", StartPath, null, workspace)
        {
            
        }

        public override ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new TypeAssistanceVhdl(editor, this);
        }

        protected override IEnumerable<ErrorListItem> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
        {
            if (file is IProjectFile pf && pf.TopFolder?.Search(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip", false) != null)
                return new List<ErrorListItem>();

            return base.ConvertErrors(pdp, file);
        }
    }
}