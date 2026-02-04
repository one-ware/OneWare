using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using IFile = OneWare.Essentials.Models.IFile;

namespace OneWare.Vhdl;

public class LanguageServiceVhdl(string workspace, ISettingsService settingsService, IPackageManager packageService)
    : LanguageServiceLspAutoDownload(settingsService.GetSettingObservable<string>(VhdlModule.LspPathSetting),
        VhdlModule.RustHdlPackage,
        VhdlModule.LspName, workspace, packageService,
        settingsService.GetSettingObservable<bool>("Experimental_AutoDownloadBinaries"))
{
    public override ITypeAssistance GetTypeAssistance(IEditor editor)
    {
        return new TypeAssistanceVhdl(editor, this, settingsService);
    }

    protected override IEnumerable<ErrorListItem> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
    {
        if (file is IProjectFile pf &&
            pf.TopFolder?.SearchName(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip") != null)
            return new List<ErrorListItem>();

        return base.ConvertErrors(pdp, file);
    }
}
