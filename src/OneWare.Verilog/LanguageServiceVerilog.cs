using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Verilog;

public class LanguageServiceVerilog(string workspace, ISettingsService settingsService, IPackageService packageService)
    : LanguageServiceLspAutoDownload(settingsService.GetSettingObservable<string>(VerilogModule.LspPathSetting),
        VerilogModule.VeriblePackage,
        VerilogModule.LspName, workspace, packageService,
        settingsService.GetSettingObservable<bool>("Experimental_AutoDownloadBinaries"))
{
    public override ITypeAssistance GetTypeAssistance(IEditor editor)
    {
        return new TypeAssistanceVerilog(editor, this, settingsService);
    }
}
