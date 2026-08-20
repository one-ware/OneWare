using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.TypeScript;

public class LanguageServiceTypeScript : LanguageServiceLspAutoDownload
{
    public LanguageServiceTypeScript(string workspace, ISettingsService settingsService,
        IPackageService packageService)
        : base(settingsService.GetSettingObservable<string>(TypeScriptModule.LspPathSetting),
            TypeScriptModule.TsgoPackage, TypeScriptModule.LspName, workspace, packageService,
            settingsService.GetSettingObservable<bool>("Experimental_AutoDownloadBinaries"),
            arguments: "--lsp -stdio")
    {
    }

    public override ITypeAssistance GetTypeAssistance(IEditor editor)
    {
        return new TypeAssistanceTypeScript(editor, this);
    }
}
