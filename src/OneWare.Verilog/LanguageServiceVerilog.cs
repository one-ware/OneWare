using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using IFile = OneWare.Essentials.Models.IFile;

namespace OneWare.Verilog;

public class LanguageServiceVerilog(string workspace, ISettingsService settingsService, IPackageService packageService)
    : LanguageServiceLspAutoDownload(settingsService.GetSettingObservable<string>(VerilogModule.LspPathSetting),
        VerilogModule.VeriblePackage,
        VerilogModule.LspName, workspace, packageService,
        settingsService.GetSettingObservable<bool>("Experimental_AutoDownloadBinaries"))
{
    public override ITypeAssistance GetTypeAssistance(IEditor editor)
    {
        return new TypeAssistanceVerilog(editor, this);
    }

    protected override IEnumerable<ErrorListItem> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
    {
        if (file is IProjectFile pf &&
            pf.TopFolder?.SearchName(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip") != null)
            return new List<ErrorListItem>();

        return base.ConvertErrors(pdp, file);
    }
}