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

namespace OneWare.Vhdl;

internal class TypeAssistanceVhdl : TypeAssistanceLanguageService
{
    private static List<TextMateSnippet>? _snippets;
        
    public TypeAssistanceVhdl(IEditor editor, LanguageServiceVhdl ls) : base(editor, ls)
    {
        CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new VhdlIndentationStrategy(CodeBox.Options);
        FormattingStrategy = new VhdlFormatter();
        FoldingStrategy = new RegexFoldingStrategy(FoldingRegexVhdl.FoldingStart, FoldingRegexVhdl.FoldingEnd);
        LineCommentSequence = "--";
        
        _snippets ??= TextMateSnippetHelper.ParseVsCodeSnippets("avares://OneWare.Vhdl/Assets/vhdl.json");
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
            AutoIndent(line, line);
        }
    }
}