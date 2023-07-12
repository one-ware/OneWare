using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Ioc;
using IFile = OneWare.Shared.IFile;

namespace OneWare.Verilog
{
    public class LanguageServiceVerilog : LanguageService
    {
        public LanguageServiceVerilog(string workspace, IPaths paths) : base ("VHDL LS", 
            Path.Combine(paths.PackagesDirectory, "verible-v0.0-3365-g76cc3fad", "bin", "verible-verilog-ls" + Platform.ExecutableExtension), null, workspace)
        {
            
        }

        public override ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new TypeAssistanceVerilog(editor, this);
        }
        
        public override async Task ActivateAsync()
        {
            if (!File.Exists(ExecutablePath) && RuntimeInformation.OSArchitecture is not Architecture.Wasm)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await ContainerLocator.Container.Resolve<IHttpService>().DownloadAndExtractArchiveAsync(
                        "https://github.com/chipsalliance/verible/releases/download/v0.0-3365-g76cc3fad/verible-v0.0-3365-g76cc3fad-win64.zip",
                        ContainerLocator.Container.Resolve<IPaths>().PackagesDirectory);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    await ContainerLocator.Container.Resolve<IHttpService>().DownloadAndExtractArchiveAsync(
                        "https://github.com/chipsalliance/verible/releases/download/v0.0-3365-g76cc3fad/verible-v0.0-3365-g76cc3fad-linux-static-x86_64.tar.gz",
                        ContainerLocator.Container.Resolve<IPaths>().PackagesDirectory);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    
                }
            }
                
            await base.ActivateAsync();
        }

        public override IEnumerable<ErrorListItemModel> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
        {
            if (file is IProjectFile pf && pf.TopFolder?.Search(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip", false) != null)
                return new List<ErrorListItemModel>();

            return base.ConvertErrors(pdp, file);
        }
    }
}