using System.Text.RegularExpressions;
using Avalonia.Input;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.ViewModels;
using OneWare.Verilog.Folding;

namespace OneWare.Verilog
{
    internal class TypeAssistanceVerilog : TypeAssistanceLanguageService
    {
        private readonly Regex _usedWordsRegex = new(@"\w{3,}");
        
        public TypeAssistanceVerilog(IEditor editor, LanguageServiceVerilog ls) : base(editor, ls)
        {
            CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new LspIndentationStrategy(CodeBox.Options, ls, CurrentFile);
            FoldingStrategy = new RegexFoldingStrategy(FoldingRegexVerilog.FoldingStart, FoldingRegexVerilog.FoldingEnd);

            LineCommentSequence = "//";
        }

        public override async Task<List<CompletionData>> GetCustomCompletionItemsAsync()
        {
            var items = new List<CompletionData>();

            var text = Editor.CurrentDocument.Text;
            var usedWords = await Task.Run(() => _usedWordsRegex.Matches(text).Select(x => x.ToString()).Distinct());
            
            foreach (var word in usedWords)
            {
                items.Add(new CompletionData(word, word, "Used word in document", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Snippet], 0, CodeBox.CaretOffset));
            }

            return items;
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
}