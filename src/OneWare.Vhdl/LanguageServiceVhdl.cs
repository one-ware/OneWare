using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Models;
using IFile = OneWare.Shared.IFile;

namespace OneWare.Vhdl
{
    public class LanguageServiceVhdl : LanguageService
    {
        public LanguageServiceVhdl(string workspace) : base ("VHDL LS", new Uri("wss://oneware-cloud-ls-vhdl-qtuhvc77rq-ew.a.run.app"), workspace)
        {
            
        }
        
        public LanguageServiceVhdl(string workspace, string executablePath) : base ("VHDL LS", executablePath, null, workspace)
        {
            
        }
        
        public override ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new TypeAssistanceVhdl(editor, this);
        }

        public override IEnumerable<ErrorListItemModel> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
        {
            if (file is IProjectFile pf && pf.TopFolder?.Search(Path.GetFileNameWithoutExtension(file.FullPath) + ".qip", false) != null)
                return new List<ErrorListItemModel>();

            return base.ConvertErrors(pdp, file);
        }
    }
}