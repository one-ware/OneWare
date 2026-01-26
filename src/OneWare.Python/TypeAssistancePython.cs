using AvaloniaEdit.Indentation.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Python;

internal class TypeAssistancePython : TypeAssistanceLanguageService
{
    public TypeAssistancePython(IEditor editor, LanguageServicePython ls) : base(editor, ls)
    {
        CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new LspIndentationStrategy(CodeBox.Options, ls, editor.CurrentFile!);
        LineCommentSequence = "//";
    }

    public override bool CanAddBreakPoints => false;
}