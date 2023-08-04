using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
using Prism.Ioc;

namespace OneWare.Cpp
{
    public class LanguageServiceCpp : LanguageService
    {
        public LanguageServiceCpp(ISettingsService settingsService, IPaths paths) : base("Clang",
            Path.Combine(paths.PackagesDirectory, "clangd_16.0.2", "bin", $"clangd"), 
            "--log=error",
            null)
        {
            // Global.Options.WhenAnyValue(x => x.CppLspNiosMode).Subscribe(x =>
            // {
            //     if (IsActivated) _ = RestartAsync();
            // });
            //t
            // Global.Options.WhenAnyValue(x => x.CppLspActivated).Subscribe(x =>
            // {
            //     //Check if file is open
            //     var anyFile = MainDock.OpenDocuments.Any(
            //         keyValuePair => SupportedFileTypes.Contains(keyValuePair.Key.Type) && Path.GetFullPath(keyValuePair.Key.FullPath).StartsWith(Path.GetFullPath(workspace)));
            //     if (anyFile && x && !IsActivated) _ = ActivateAsync();
            //     else if (!x && IsActivated) _ = DeactivateAsync();
            // });
        }

        public override ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new TypeAssistanceCpp(editor, this);
        }

        public override async Task ActivateAsync()
        {
            if (!File.Exists(ExecutablePath) && RuntimeInformation.OSArchitecture is not Architecture.Wasm)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await ContainerLocator.Container.Resolve<IHttpService>().DownloadAndExtractArchiveAsync(
                        "https://github.com/clangd/clangd/releases/download/16.0.2/clangd-windows-16.0.2.zip",
                        ContainerLocator.Container.Resolve<IPaths>().PackagesDirectory);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                { 
                    await ContainerLocator.Container.Resolve<IHttpService>().DownloadAndExtractArchiveAsync(
                    "https://github.com/clangd/clangd/releases/download/16.0.2/clangd-mac-16.0.2.zip",
                    ContainerLocator.Container.Resolve<IPaths>().PackagesDirectory);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                { 
                    await ContainerLocator.Container.Resolve<IHttpService>().DownloadAndExtractArchiveAsync(
                        "https://github.com/clangd/clangd/releases/download/16.0.2/clangd-linux-16.0.2.zip",
                        ContainerLocator.Container.Resolve<IPaths>().PackagesDirectory);
                }
            }
            await base.ActivateAsync();
        }
    }

    public class CustomCppInitialisationOptions
    {
        public Container<string>? FallbackFlags { get; set; }

        public string? CompilationDatabasePath { get; set; }
    }
}