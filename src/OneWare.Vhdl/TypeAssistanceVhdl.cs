using Avalonia.Input;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Settings;
using OneWare.Vhdl.Folding;
using OneWare.Vhdl.Formatting;
using OneWare.Vhdl.Indentation;

namespace OneWare.Vhdl;

internal class TypeAssistanceVhdl : TypeAssistanceLanguageService
{
    private static List<TextMateSnippet>? _snippets;

    private readonly ISettingsService _settingsService;

    public TypeAssistanceVhdl(IEditor editor, LanguageServiceVhdl ls, ISettingsService settingsService) : base(editor,
        ls)
    {
        CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new VhdlIndentationStrategy(CodeBox.Options);
        FormattingStrategy = new VhdlFormatter();
        FoldingStrategy = new RegexFoldingStrategy(FoldingRegexVhdl.FoldingStart, FoldingRegexVhdl.FoldingEnd);
        LineCommentSequence = "--";

        _snippets ??= TextMateSnippetHelper.ParseVsCodeSnippets("avares://OneWare.Vhdl/Assets/vhdl.json");

        _settingsService = settingsService;
    }

    protected override Task ShowCompletionAsync(CompletionTriggerKind triggerKind, string? triggerChar)
    {
        if (IsInComment(CodeBox.CaretOffset)) return Task.CompletedTask;

        return base.ShowCompletionAsync(triggerKind, triggerChar);
    }

    protected override Task<List<CompletionData>> GetCustomCompletionItemsAsync()
    {
        var items = new List<CompletionData>();

        if (_settingsService.GetSettingValue<bool>(VhdlModule.EnableSnippetsSetting) && _snippets != null)
        {
            items.AddRange(_snippets.Select(snippet => new CompletionData(snippet.Content, snippet.Label, null,
                snippet.Description, TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Snippet], 0,
                CodeBox.CaretOffset)));
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