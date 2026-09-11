using Avalonia.Input;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.ViewModels;
using OneWare.Verilog.Indentation;

namespace OneWare.Verilog;

internal class TypeAssistanceVerilog : TypeAssistanceLanguageService
{
    public TypeAssistanceVerilog(IEditor editor, LanguageServiceVerilog ls) : base(editor, ls)
    {
        CodeBox.TextArea.IndentationStrategy =
            IndentationStrategy = new VerilogIndentationStrategy(CodeBox.Options);
        FormattingStrategy = new LspFormattingStrategy(ls, editor.FullPath);
        FoldingStrategy = new LspFoldingStrategy(ls, editor.FullPath);

        LineCommentSequence = "//";
    }

    protected override void TextEnteredAutoFormat(TextInputEventArgs e)
    {
        if ((e.Text?.Contains(';') ?? false) && Service.IsLanguageServiceReady)
        {
            var line = CodeBox.Document.GetLineByOffset(CodeBox.CaretOffset).LineNumber;
            //AutoIndent(line, line);
        }
    }
}
