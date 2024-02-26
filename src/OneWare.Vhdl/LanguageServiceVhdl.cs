using System.Reflection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using IFile = OneWare.Essentials.Models.IFile;

namespace OneWare.Vhdl
{
    public class LanguageServiceVhdl(string workspace, INativeToolService nativeToolService, ISettingsService settingsService)
        : LanguageServiceLspAutoDownload(settingsService.GetSettingObservable<string>(VhdlModule.LspPathSetting),
            () => nativeToolService.InstallAsync(nativeToolService.Get(VhdlModule.LspName)!),
            VhdlModule.LspName, workspace)
    {
        public override ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new TypeAssistanceVhdl(editor, this);
        }

        protected override IEnumerable<ErrorListItem> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
        {
            if (file is IProjectFile pf && pf.TopFolder?.Search(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip", false) != null)
                return new List<ErrorListItem>();

            return base.ConvertErrors(pdp, file);
        }
    }
}