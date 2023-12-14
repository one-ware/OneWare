using System.Reflection;
using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.SDK.Helpers;
using OneWare.SDK.LanguageService;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;
using Prism.Ioc;

namespace OneWare.Cpp
{
    public class LanguageServiceCpp : LanguageServiceLspAutoDownload
    {
        public LanguageServiceCpp(INativeToolService nativeToolService, ISettingsService settingsService) 
            : base(settingsService.GetSettingObservable<string>(CppModule.LspPathSetting), () => nativeToolService.InstallAsync(nativeToolService.Get(CppModule.LspName)!), 
                CppModule.LspName, null)
        {
            Arguments = "--log=error";
                
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