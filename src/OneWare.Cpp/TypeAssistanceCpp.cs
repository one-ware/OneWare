using AvaloniaEdit.Indentation.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Cpp.Folding;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Cpp;

internal class TypeAssistanceCpp : TypeAssistanceLanguageService
{
    private readonly IErrorService _errorService;

    public TypeAssistanceCpp(
        IEditor editor,
        LanguageServiceCpp ls,
        IErrorService errorService) : base(editor, ls)
    {
        _errorService = errorService;

        CodeBox.TextArea.IndentationStrategy = IndentationStrategy =
            new CSharpIndentationStrategy(CodeBox.Options);

        FoldingStrategy = new RegexFoldingStrategy(FoldingRegexCpp.FoldingStart, FoldingRegexCpp.FoldingEnd);

        LineCommentSequence = "//";
    }

    public override bool CanAddBreakPoints => true;

    public override async Task<string?> GetHoverInfoAsync(int offset)
    {
        if (!Service.IsLanguageServiceReady) return null;

        var pos = CodeBox.Document.GetLocation(offset);

        var error = _errorService.GetErrorsForFile(Editor.CurrentFile!)
            .OrderBy(x => x.Type)
            .FirstOrDefault(e =>
                pos.Line >= e.StartLine &&
                pos.Line <= e.EndLine &&
                pos.Column >= e.StartColumn &&
                pos.Column <= e.EndColumn);

        var info = "";

        if (error != null)
            info += error.Description + "\n";

        var hover = await Service.RequestHoverAsync(CurrentFile.FullPath,
            new Position(pos.Line - 1, pos.Column - 1));

        if (hover != null)
        {
            if (hover.Contents.HasMarkedStrings)
                info += hover.Contents.MarkedStrings!.First().Value.Split('\n')[0];

            if (hover.Contents.HasMarkupContent)
                info += $"```cpp\n{hover.Contents.MarkupContent?.Value}\n```";
        }

        return string.IsNullOrWhiteSpace(info) ? null : info;
    }
}
