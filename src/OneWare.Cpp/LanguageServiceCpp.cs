using System.Reflection;
using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared.Helpers;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
using Prism.Ioc;

namespace OneWare.Cpp
{
    public class LanguageServiceCpp : LanguageService
    {
        private static readonly string? StartPath;
        
        static LanguageServiceCpp()
        {
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            
            StartPath = PlatformHelper.Platform switch
            {
                PlatformId.WinX64 => $"{assemblyPath}/native_tools/win-x64/clangd/clangd_16.0.2/bin/clangd.exe",
                PlatformId.LinuxX64 => $"{assemblyPath}/native_tools/linux-x64/clangd/clangd_16.0.2/bin/clangd",
                PlatformId.OsxX64 => $"{assemblyPath}/native_tools/osx-x64/clangd/clangd_16.0.2/bin/clangd",
                PlatformId.OsxArm64 => $"{assemblyPath}/native_tools/osx-x64/clangd/clangd_16.0.2/bin/clangd",
                _ => null
            };
        }
        
        public LanguageServiceCpp(ISettingsService settingsService, IPaths paths) : base("Clang", StartPath,"--log=error",null)
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
    }

    public class CustomCppInitialisationOptions
    {
        public Container<string>? FallbackFlags { get; set; }

        public string? CompilationDatabasePath { get; set; }
    }
}