using System.Text.RegularExpressions;
using Avalonia.Input;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.SDK.EditorExtensions;
using OneWare.SDK.LanguageService;
using OneWare.SDK.ViewModels;
using OneWare.Vhdl.Folding;
using OneWare.Vhdl.Formatting;
using OneWare.Vhdl.Indentation;

namespace OneWare.Vhdl
{
    internal class TypeAssistanceVhdl : TypeAssistanceLsp
    {
        private readonly Regex _usedWordsRegex = new(@"\w{3,}");
        
        public TypeAssistanceVhdl(IEditor editor, LanguageServiceVhdl ls) : base(editor, ls)
        {
            CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new VhdlIndentationStrategy(CodeBox.Options);
            FormattingStrategy = new VhdlFormatter();
            FoldingStrategy = new RegexFoldingStrategy(FoldingRegexVhdl.FoldingStart, FoldingRegexVhdl.FoldingEnd);
            LineCommentSequence = "--";
        }

        public override async Task<List<CompletionData>> GetCustomCompletionItemsAsync()
        {
            var items = new List<CompletionData>();

            var text = Editor.CurrentDocument.Text;

            items.Add(new CompletionData("library IEEE;\nuse IEEE.std_logic_1164.all;\nuse IEEE.numeric_std.all; ",
                "ieee", "IEEE Standard Packages",
                TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Reference], 0, CodeBox.CaretOffset));
            
            items.Add(new CompletionData(
                "entity " + Path.GetFileNameWithoutExtension(Editor.CurrentFile.Header) +
                " is\n    port(\n        [I/Os]$0\n    );\nend entity " +
                Path.GetFileNameWithoutExtension(Editor.CurrentFile.Header) + ";", "entity", "Entity Declaration",
                TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Class], 0, CodeBox.CaretOffset));
            
            var usedWords = await Task.Run(() => _usedWordsRegex.Matches(text).Select(x => x.ToString()).Distinct());
            
            foreach (var word in usedWords)
            {
                items.Add(new CompletionData(word, word, "Used word in document", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Snippet], 0, CodeBox.CaretOffset));
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