using AvaloniaEdit.Indentation.CSharp;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.ViewModels;
using OneWare.TypeScript.Folding;

namespace OneWare.TypeScript;

internal class TypeAssistanceTypeScript : TypeAssistanceLanguageService
{
    public TypeAssistanceTypeScript(IEditor editor, LanguageServiceTypeScript ls) : base(editor, ls)
    {
        CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new CSharpIndentationStrategy(CodeBox.Options);
        FoldingStrategy = new RegexFoldingStrategy(FoldingRegexTypeScript.FoldingStart,
            FoldingRegexTypeScript.FoldingEnd);
        LineCommentSequence = "//";
    }

    public override bool CanAddBreakPoints => false;
}
