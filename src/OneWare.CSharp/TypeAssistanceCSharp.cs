using AvaloniaEdit.Indentation.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.CSharp;

internal class TypeAssistanceCSharp : TypeAssistanceLanguageService
{
    public TypeAssistanceCSharp(IEditor editor, LanguageServiceCSharp ls) : base(editor, ls)
    {
        CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new CSharpIndentationStrategy(CodeBox.Options);
        LineCommentSequence = "//";
    }

    public override bool CanAddBreakPoints => true;
}
