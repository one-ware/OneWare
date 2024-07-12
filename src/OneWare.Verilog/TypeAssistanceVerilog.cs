using System.Text.RegularExpressions;
using Avalonia.Input;
using Avalonia.Platform;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.ViewModels;
using OneWare.Verilog.Folding;
using Prism.DryIoc.Properties;

namespace OneWare.Verilog
{
    internal class TypeAssistanceVerilog : TypeAssistanceLanguageService
    {
        private static List<TextMateSnippet>? _snippets;
        
        public TypeAssistanceVerilog(IEditor editor, LanguageServiceVerilog ls) : base(editor, ls)
        {
            CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new LspIndentationStrategy(CodeBox.Options, ls, CurrentFile);
            FoldingStrategy = new RegexFoldingStrategy(FoldingRegexVerilog.FoldingStart, FoldingRegexVerilog.FoldingEnd);

            LineCommentSequence = "//";
        
            _snippets ??= TextMateSnippetHelper.ParseVsCodeSnippets("avares://OneWare.Verilog/Assets/verilog.json");
        }

        public override Task<List<CompletionData>> GetCustomCompletionItemsAsync()
        {
            var items = new List<CompletionData>();

            if (_snippets != null)
            {
                items.AddRange(_snippets.Select(snippet => new CompletionData(snippet.Content, snippet.Label, null, snippet.Description, TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Snippet], 0, CodeBox.CaretOffset)));
            }

            return Task.FromResult(items);
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