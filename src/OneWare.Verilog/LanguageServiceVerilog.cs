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

    protected override IEnumerable<ErrorListItem> ConvertErrors(PublishDiagnosticsParams pdp, string fullPath)
    {
        var entry = ContainerLocator.Container.Resolve<IProjectExplorerService>()
            .GetEntryFromFullPath(fullPath) as IProjectFile;

        if (entry is not null &&
            entry.TopFolder?.GetFile(Path.GetFileNameWithoutExtension(fullPath) + ".qip") != null)
            return new List<ErrorListItem>();

        return base.ConvertErrors(pdp, fullPath);
    }
}
