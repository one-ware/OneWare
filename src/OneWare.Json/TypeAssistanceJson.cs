using OneWare.Json.Folding;
using OneWare.Shared;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.LanguageService;

namespace OneWare.Json;

public class TypeAssistanceJson : TypeAssistance
{
    public TypeAssistanceJson(IEditor editor) : base(editor)
    {
        FoldingStrategy = new FoldingStrategyJson();
    }
}