using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.ViewModels;
using OneWare.Json.Folding;
using OneWare.Json.Formatting;

namespace OneWare.Json;

public class TypeAssistanceJson : TypeAssistanceBase
{
    public TypeAssistanceJson(IEditor editor) : base(editor)
    {
        FoldingStrategy = new RegexFoldingStrategy(FoldingRegexJson.FoldingStart, FoldingRegexJson.FoldingEnd);
        FormattingStrategy = new JsonFormatter();
    }
}