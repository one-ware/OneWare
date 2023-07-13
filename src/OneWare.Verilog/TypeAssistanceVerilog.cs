using System.Text.RegularExpressions;
using Avalonia.Input;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.LanguageService;
using OneWare.Verilog.Folding;

namespace OneWare.Verilog
{
    internal class TypeAssistanceVerilog : TypeAssistanceLsp
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
            var usedWords = await Task.Run(() => _usedWordsRegex.Matches(text));
            
            foreach (var word in usedWords)
            {
                if(word.ToString() is { } s)
                    items.Add(new CompletionData(s, s, "Used word in document", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Snippet], 0, CodeBox.CaretOffset));
            }

            return items;
        }

        public override void TypeAssistance(TextInputEventArgs e)
        {
            if ((e.Text?.Contains(';') ?? false) && Service.IsLanguageServiceReady)
            {
                var line = CodeBox.Document.GetLineByOffset(CodeBox.CaretOffset).LineNumber;
                AutoIndent(line, line);
            }
        }
    }
}