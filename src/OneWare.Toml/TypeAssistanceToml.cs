using OneWare.Shared.LanguageService;
using OneWare.Shared.ViewModels;

namespace OneWare.Toml;

public class TypeAssistanceToml : TypeAssistance
{
    public TypeAssistanceToml(IEditor editor) : base(editor)
    {
        LineCommentSequence = "#";
    }
}