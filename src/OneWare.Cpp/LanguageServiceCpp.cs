using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Cpp;

public class LanguageServiceCpp : LanguageServiceLspAutoDownload
{
    public LanguageServiceCpp(ISettingsService settingsService, IPackageService packageService)
        : base(settingsService.GetSettingObservable<string>(CppModule.LspPathSetting), CppModule.ClangdPackage,
            CppModule.LspName, null, packageService,
            settingsService.GetSettingObservable<bool>("Experimental_AutoDownloadBinaries"),
            arguments: "--log=error")
    {
        // ...existing code...
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
