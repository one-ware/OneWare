using System.Reflection;
using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared.Helpers;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
using Prism.Ioc;
using IFile = OneWare.Shared.Models.IFile;

namespace OneWare.Verilog
{
    public class LanguageServiceVerilog : LanguageService
    {
        private static readonly string? StartPath;
        
        static LanguageServiceVerilog()
        {
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            
            StartPath = PlatformHelper.Platform switch
            {
                PlatformId.WinX64 => $"{assemblyPath}/verible/verible-v0.0-3401-g0b8cb4e0-win64/verible-verilog-ls.exe",
                PlatformId.LinuxX64 => $"{assemblyPath}/verible/verible-v0.0-3401-g0b8cb4e0/bin/verible-verilog-ls",
                PlatformId.OsxX64 => $"{assemblyPath}/verible/verible-v0.0-3401-g0b8cb4e0-macOS/bin/verible-verilog-ls",
                PlatformId.OsxArm64 => $"{assemblyPath}/verible/verible-v0.0-3401-g0b8cb4e0-macOS/bin/verible-verilog-ls",
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

        public override IEnumerable<ErrorListItemModel> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
        {
            if (file is IProjectFile pf && pf.TopFolder?.Search(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip", false) != null)
                return new List<ErrorListItemModel>();

            return base.ConvertErrors(pdp, file);
        }
    }
}