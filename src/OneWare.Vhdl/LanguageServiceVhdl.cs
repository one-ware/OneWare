using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Vhdl;

public class LanguageServiceVhdl(string workspace, ISettingsService settingsService, IPackageService packageService)
    : LanguageServiceLspAutoDownload(settingsService.GetSettingObservable<string>(VhdlModule.LspPathSetting),
        VhdlModule.RustHdlPackage,
        VhdlModule.LspName, workspace, packageService,
        settingsService.GetSettingObservable<bool>("Experimental_AutoDownloadBinaries"))
{
    public override ITypeAssistance GetTypeAssistance(IEditor editor)
    {
        return new TypeAssistanceVhdl(editor, this, settingsService);
    }
}
