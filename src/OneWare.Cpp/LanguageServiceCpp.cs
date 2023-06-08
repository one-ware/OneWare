using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.LanguageService;

namespace OneWare.Cpp
{
    public class LanguageServiceCpp : LanguageService
    {
        public LanguageServiceCpp() : base("CPP LS", new Uri("wss://oneware-cloud-ls-clangd-qtuhvc77rq-ew.a.run.app"),
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

        public LanguageServiceCpp(string executablePath) : base ("CPP LS", executablePath, null, null)
        {
            
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