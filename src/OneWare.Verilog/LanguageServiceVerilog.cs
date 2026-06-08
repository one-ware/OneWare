using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Verilog;

public class LanguageServiceVerilog : LanguageServiceLspAutoDownload
{
    private readonly ISettingsService _settingsService;

    public LanguageServiceVerilog(string workspace, ISettingsService settingsService, IPackageService packageService)
        : base(settingsService.GetSettingObservable<string>(VerilogModule.LspPathSetting),
            VerilogModule.VeriblePackage,
            VerilogModule.LspName, workspace, packageService,
            settingsService.GetSettingObservable<bool>("Experimental_AutoDownloadBinaries"),
            arguments: "--rules=-no-trailing-spaces")
    {
        _settingsService = settingsService;
    }

    public override ITypeAssistance GetTypeAssistance(IEditor editor)
    {
        return new TypeAssistanceVerilog(editor, this, _settingsService);
    }
}
