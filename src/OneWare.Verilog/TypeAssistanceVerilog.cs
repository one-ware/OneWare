using Avalonia.Input;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Verilog.Folding;

namespace OneWare.Verilog;

internal class TypeAssistanceVerilog : TypeAssistanceLanguageService
{
    private static List<TextMateSnippet>? _snippets;
    private readonly ISettingsService _settingsService;

    public TypeAssistanceVerilog(IEditor editor, LanguageServiceVerilog ls, ISettingsService settingsService) :
        base(editor, ls)
    {
        _settingsService = settingsService;

        CodeBox.TextArea.IndentationStrategy =
            IndentationStrategy = new LspIndentationStrategy(CodeBox.Options, ls, CurrentFilePath);
        FoldingStrategy = new RegexFoldingStrategy(FoldingRegexVerilog.FoldingStart, FoldingRegexVerilog.FoldingEnd);

        LineCommentSequence = "//";

        _snippets ??= TextMateSnippetHelper.ParseVsCodeSnippets("avares://OneWare.Verilog/Assets/verilog.json");
    }

    protected override Task<List<CompletionData>> GetCustomCompletionItemsAsync()
    {
        var items = new List<CompletionData>();

        if (IsInComment(CodeBox.CaretOffset)) return Task.FromResult(items);

        if (_settingsService.GetSettingValue<bool>(VerilogModule.EnableSnippetsSetting) && _snippets != null)
            items.AddRange(_snippets.Select(snippet => new CompletionData(snippet.Content, snippet.Label, null,
                snippet.Description, TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Snippet], 0,
                CodeBox.CaretOffset, CurrentFilePath)));

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

    private bool IsInComment(int position)
    {
        if (position < 0 || position > CodeBox.Document.TextLength) return false;

        // Check for single line comments by searching backwards to the start of the line
        var line = CodeBox.Document.GetLineByOffset(position);
        var text = CodeBox.Document.GetText(line);
        var index = CodeBox.CaretOffset - line.Offset;
        var commentIndex = text.IndexOf(LineCommentSequence!, 0, index, StringComparison.Ordinal);
        if (commentIndex != -1) return true;

        // Check for multiline comments by searching backwards and forwards
        var multiLineStart = CodeBox.Document.Text.LastIndexOf("/*", position, StringComparison.Ordinal);
        var multiLineEnd = CodeBox.Document.Text.IndexOf("*/", position, StringComparison.Ordinal);

        if (multiLineStart != -1 && multiLineEnd != -1 && multiLineStart < position &&
            position < multiLineEnd + 2) return true;

        return false;
    }
}
