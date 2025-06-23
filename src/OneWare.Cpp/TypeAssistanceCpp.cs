using AvaloniaEdit.Indentation.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Cpp.Folding;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Microsoft.Extensions.Logging; // Make sure you have this using directive for ILogger

namespace OneWare.Cpp;

internal class TypeAssistanceCpp : TypeAssistanceLanguageService
{
    private readonly IErrorService _errorService;

    // IMPORTANT: All parameters required by TypeAssistanceLanguageService's constructor
    // must be accepted here and passed to the base constructor in the correct order.
    public TypeAssistanceCpp(
        IEditor editor,
        LanguageServiceCpp ls, // This corresponds to ILanguageService langService at the end
        IErrorService errorService, // Corresponds to IErrorService errorService
        ILogger<TypeAssistanceLanguageService> logger, // Corresponds to ILogger<TypeAssistanceLanguageService> logger
        IProjectExplorerService projectExplorerService, // Corresponds to IProjectExplorerService
        ISettingsService settingsService, // Corresponds to ISettingsService
        IDockService dockService, // Corresponds to IDockService
        ILanguageManager languageManager) // Corresponds to ILanguageManager
        : base(editor, logger, errorService, projectExplorerService, settingsService, dockService, languageManager, ls) // PASS ALL 8 PARAMETERS IN ORDER
    {
        _errorService = errorService; // You keep this for internal use in TypeAssistanceCpp if needed

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