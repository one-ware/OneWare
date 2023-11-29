using System.Reflection;
using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.SDK.Helpers;
using OneWare.SDK.LanguageService;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;
using Prism.Ioc;
using IFile = OneWare.SDK.Models.IFile;

namespace OneWare.Verilog
{
    public class LanguageServiceVerilog : LanguageServiceLsp
    {
        private static readonly string? StartPath;
        
        static LanguageServiceVerilog()
        {
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            
            StartPath = PlatformHelper.Platform switch
            {
                PlatformId.WinX64 => $"{assemblyPath}/native_tools/win-x64/verible/verible-v0.0-3428-gcfcbb82b-win64/verible-verilog-ls.exe",
                PlatformId.LinuxX64 => $"{assemblyPath}/native_tools/linux-x64/verible/verible-v0.0-3428-gcfcbb82b/bin/verible-verilog-ls",
                PlatformId.OsxX64 => $"{assemblyPath}/native_tools/osx-x64/verible/verible-v0.0-3426-gac4a37d8-macOS/bin/verible-verilog-ls",
                PlatformId.OsxArm64 => $"{assemblyPath}/native_tools/osx-x64/verible/verible-v0.0-3426-gac4a37d8-macOS/bin/verible-verilog-ls",
                _ => null
            };
        }
        
        public LanguageServiceVerilog(string workspace) : base ("Verible", StartPath, null, workspace)
        {
            
        }

        public override ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new TypeAssistanceVerilog(editor, this);
        }

        protected override IEnumerable<ErrorListItem> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
        {
            if (file is IProjectFile pf && pf.TopFolder?.Search(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip", false) != null)
                return new List<ErrorListItem>();

            return base.ConvertErrors(pdp, file);
        }
    }
}