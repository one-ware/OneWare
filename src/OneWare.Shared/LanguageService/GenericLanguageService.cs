using OneWare.Shared.ViewModels;

namespace OneWare.Shared.LanguageService
{
    public class GenericLanguageService : LanguageService
    {
        public GenericLanguageService(string name, string executablePath, string arguments, string? workspace) : base (name, executablePath, arguments, workspace)
        {
        }

        public override ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new GenericTypeAssistanceLsp(editor, this);
        }
    }
}