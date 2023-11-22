using OneWare.SDK.LanguageService;
using OneWare.SDK.ViewModels;

namespace OneWare.Toml;

public class TypeAssistanceToml : TypeAssistance
{
    public TypeAssistanceToml(IEditor editor) : base(editor)
    {
        LineCommentSequence = "#";
    }
}