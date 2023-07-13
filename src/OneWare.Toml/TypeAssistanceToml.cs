using OneWare.Shared;
using OneWare.Shared.LanguageService;

namespace OneWare.Toml;

public class TypeAssistanceToml : TypeAssistance
{
    public TypeAssistanceToml(IEditor editor) : base(editor)
    {
        LineCommentSequence = "#";
    }
}