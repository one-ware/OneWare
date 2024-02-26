using OneWare.Essentials.LanguageService;
using OneWare.Essentials.ViewModels;

namespace OneWare.Toml;

public class TypeAssistanceToml : TypeAssistanceBase
{
    public TypeAssistanceToml(IEditor editor) : base(editor)
    {
        LineCommentSequence = "#";
    }
}