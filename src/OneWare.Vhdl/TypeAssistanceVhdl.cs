using System.Text.RegularExpressions;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.ViewModels;
using OneWare.Vhdl.Folding;
using OneWare.Vhdl.Formatting;
using OneWare.Vhdl.Indentation;

namespace OneWare.Vhdl
{
    internal partial class TypeAssistanceVhdl : TypeAssistanceLanguageService
    {
        public TypeAssistanceVhdl(IEditor editor, LanguageServiceVhdl ls) : base(editor, ls)
        {
            CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new VhdlIndentationStrategy(CodeBox.Options);
            FormattingStrategy = new VhdlFormatter();
            FoldingStrategy = new RegexFoldingStrategy(FoldingRegexVhdl.FoldingStart, FoldingRegexVhdl.FoldingEnd);
            LineCommentSequence = "--";
        }

        protected override void TextEnteredAutoFormat(TextInputEventArgs e)
        {
            if ((e.Text?.Contains(';') ?? false) && Service.IsLanguageServiceReady)
            {
                var line = CodeBox.Document.GetLineByOffset(CodeBox.CaretOffset).LineNumber;
                AutoIndent(line, line);
            }
        }
    }
}