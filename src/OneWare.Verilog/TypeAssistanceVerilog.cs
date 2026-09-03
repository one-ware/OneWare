using Avalonia.Input;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Verilog.Indentation;

namespace OneWare.Verilog;

internal class TypeAssistanceVerilog : TypeAssistanceLanguageService
{
    private static List<TextMateSnippet>? _verilogSnippets;
    private static List<TextMateSnippet>? _systemVerilogSnippets;
    private readonly bool _isSystemVerilog;
    private readonly ISettingsService _settingsService;

    public TypeAssistanceVerilog(IEditor editor, LanguageServiceVerilog ls, ISettingsService settingsService) :
        base(editor, ls)
    {
        _settingsService = settingsService;

        CodeBox.TextArea.IndentationStrategy =
            IndentationStrategy = new VerilogIndentationStrategy(CodeBox.Options);
        FormattingStrategy = new LspFormattingStrategy(ls, editor.FullPath);
        FoldingStrategy = new LspFoldingStrategy(ls, editor.FullPath);

        LineCommentSequence = "//";
        _isSystemVerilog = VerilogModule.SystemVerilogExtensions.Contains(Path.GetExtension(editor.FullPath),
            StringComparer.OrdinalIgnoreCase);

        _verilogSnippets ??= TextMateSnippetHelper.ParseVsCodeSnippets("avares://OneWare.Verilog/Assets/verilog.json");
        if (_isSystemVerilog)
            _systemVerilogSnippets ??=
                TextMateSnippetHelper.ParseVsCodeSnippets("avares://OneWare.Verilog/Assets/systemverilog.json");
    }

    protected override Task<List<CompletionData>> GetCustomCompletionItemsAsync()
    {
        var items = new List<CompletionData>();

        if (IsInComment(CodeBox.CaretOffset)) return Task.FromResult(items);

        if (_settingsService.GetSettingValue<bool>(VerilogModule.EnableSnippetsSetting) && _verilogSnippets != null)
            items.AddRange(_verilogSnippets.Select(snippet => new CompletionData(snippet.Content, snippet.Label, null,
                snippet.Description, TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Snippet], 0,
                CodeBox.CaretOffset, CurrentFilePath)));

        if (_settingsService.GetSettingValue<bool>(VerilogModule.EnableSnippetsSetting) && _isSystemVerilog &&
            _systemVerilogSnippets != null)
            items.AddRange(_systemVerilogSnippets.Select(snippet => new CompletionData(snippet.Content, snippet.Label,
                null, snippet.Description, TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Snippet], 0,
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
