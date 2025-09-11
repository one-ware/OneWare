using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Python;

public class LanguageServicePython : LanguageServiceLsp
{
    public LanguageServicePython()
        : base(PythonModule.LspName, null)
    {
        
    }

    public override ITypeAssistance GetTypeAssistance(IEditor editor)
    {
        return new TypeAssistancePython(editor, this);
    }
}