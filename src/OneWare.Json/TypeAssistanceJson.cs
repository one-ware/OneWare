using OneWare.Json.Folding;
using OneWare.Json.Formatting;
using OneWare.SDK.EditorExtensions;
using OneWare.SDK.LanguageService;
using OneWare.SDK.ViewModels;

namespace OneWare.Json;

public class TypeAssistanceJson : TypeAssistance
{
    public TypeAssistanceJson(IEditor editor) : base(editor)
    {
        FoldingStrategy = new RegexFoldingStrategy(FoldingRegexJson.FoldingStart, FoldingRegexJson.FoldingEnd);
        FormattingStrategy = new JsonFormatter();
    }
}