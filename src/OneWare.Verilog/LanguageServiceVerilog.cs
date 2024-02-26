using System.Reflection;
using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;
using IFile = OneWare.Essentials.Models.IFile;

namespace OneWare.Verilog
{
    public class LanguageServiceVerilog(string workspace, INativeToolService nativeToolService, ISettingsService settingsService)
        : LanguageServiceLspAutoDownload(settingsService.GetSettingObservable<string>(VerilogModule.LspPathSetting),
            () => nativeToolService.InstallAsync(nativeToolService.Get(VerilogModule.LspName)!),
            VerilogModule.LspName, workspace)
    {
        public override ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new TypeAssistanceVerilog(editor, this);
        }

        protected override IEnumerable<ErrorListItem> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
        {
            if (file is IProjectFile pf && pf.TopFolder?.Search(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip", false) != null)
                return new List<ErrorListItem>();

            return base.ConvertErrors(pdp, file);
        }
    }
}