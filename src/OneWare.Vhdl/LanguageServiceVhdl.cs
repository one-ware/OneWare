using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Ioc;
using IFile = OneWare.Shared.IFile;

namespace OneWare.Vhdl
{
    public class LanguageServiceVhdl : LanguageService
    {
        public LanguageServiceVhdl(string workspace, IPaths paths) : base ("RustHDL", 
            RuntimeInformation.ProcessArchitecture == Architecture.Wasm ? "wss://oneware-cloud-ls-vhdl-qtuhvc77rq-ew.a.run.app"
            : (
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Path.Combine(paths.PackagesDirectory, "vhdl_ls-x86_64-unknown-linux-musl", "bin", "vhdl_ls")
                    : Path.Combine(paths.PackagesDirectory, "vhdl_ls-x86_64-pc-windows-msvc", "bin", "vhdl_ls.exe")
                ), null, workspace)
        {
            
        }

        public override ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new TypeAssistanceVhdl(editor, this);
        }
        
        public override async Task ActivateAsync()
        {
            if (!File.Exists(ExecutablePath) && RuntimeInformation.OSArchitecture is not Architecture.Wasm)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await ContainerLocator.Container.Resolve<IHttpService>().DownloadAndExtractArchiveAsync(
                        "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.64.0/vhdl_ls-x86_64-pc-windows-msvc.zip",
                        ContainerLocator.Container.Resolve<IPaths>().PackagesDirectory);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    await ContainerLocator.Container.Resolve<IHttpService>().DownloadAndExtractArchiveAsync(
                        "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.65.0/vhdl_ls-x86_64-unknown-linux-musl.zip",
                        ContainerLocator.Container.Resolve<IPaths>().PackagesDirectory);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    await ContainerLocator.Container.Resolve<IHttpService>().DownloadAndExtractArchiveAsync(
                        "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.65.0/vhdl_ls-x86_64-unknown-linux-musl.zip",
                        ContainerLocator.Container.Resolve<IPaths>().PackagesDirectory);
                }
                
                Tools.ChmodFolder(ContainerLocator.Container.Resolve<IPaths>().PackagesDirectory);
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