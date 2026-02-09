using AvaloniaEdit.Indentation.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Cpp.Folding;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Cpp;

internal class TypeAssistanceCpp : TypeAssistanceLanguageService
{
    public TypeAssistanceCpp(IEditor editor, LanguageServiceCpp ls) : base(editor, ls)
    {
        CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new CSharpIndentationStrategy(CodeBox.Options);
        FoldingStrategy =
            new RegexFoldingStrategy(FoldingRegexCpp.FoldingStart,
                FoldingRegexCpp.FoldingEnd); //new LspFoldingStrategy(ls, editor.FullPath);
        LineCommentSequence = "//";
    }

    public override bool CanAddBreakPoints => true;
}
