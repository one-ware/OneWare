using System.Reflection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.SDK.Helpers;
using OneWare.SDK.LanguageService;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;
using IFile = OneWare.SDK.Models.IFile;

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