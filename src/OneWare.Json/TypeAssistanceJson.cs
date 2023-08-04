using OneWare.Json.Folding;
using OneWare.Json.Formatting;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.LanguageService;
using OneWare.Shared.ViewModels;

namespace OneWare.Json;

public class TypeAssistanceJson : TypeAssistance
{
    public TypeAssistanceJson(IEditor editor) : base(editor)
    {
        FoldingStrategy = new RegexFoldingStrategy(FoldingRegexJson.FoldingStart, FoldingRegexJson.FoldingEnd);
        FormattingStrategy = new JsonFormatter();
    }
}