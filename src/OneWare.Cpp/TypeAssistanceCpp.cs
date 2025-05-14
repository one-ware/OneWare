using AvaloniaEdit.Indentation.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Cpp.Folding;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Autofac;

namespace OneWare.Cpp
{
    internal class TypeAssistanceCpp : TypeAssistanceLanguageService
    {
        private readonly IErrorService _errorService;

        // Constructor with Autofac DI
        public TypeAssistanceCpp(IEditor editor, LanguageServiceCpp ls, IErrorService errorService)
            : base(editor, ls)
        {
            _errorService = errorService;

            CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new CSharpIndentationStrategy(CodeBox.Options);
            FoldingStrategy = new RegexFoldingStrategy(FoldingRegexCpp.FoldingStart, FoldingRegexCpp.FoldingEnd);
            LineCommentSequence = "//";
        }

        public override bool CanAddBreakPoints => true;

        public override async Task<string?> GetHoverInfoAsync(int offset)
        {
            if (!Service.IsLanguageServiceReady) return null;

            var pos = CodeBox.Document.GetLocation(offset);

            // Use the injected IErrorService instead of resolving via ContainerLocator
            var error = _errorService.GetErrorsForFile(Editor.CurrentFile!)
                .OrderBy(x => x.Type)
                .FirstOrDefault(error => pos.Line >= error.StartLine
                                         && pos.Line <= error.EndLine
                                         && pos.Column >= error.StartColumn
                                         && pos.Column <= error.EndColumn);
            var info = "";

            if (error != null) info += error.Description + "\n";

            var hover = await Service.RequestHoverAsync(CurrentFile.FullPath,
                new Position(pos.Line - 1, pos.Column - 1));
            if (hover != null)
            {
                if (hover.Contents.HasMarkedStrings)
                    info += hover.Contents.MarkedStrings!.First().Value.Split('\n')[0]; // TODO: Clarify this behavior
                if (hover.Contents.HasMarkupContent) info += $"```cpp\n{hover.Contents.MarkupContent?.Value}\n```";
            }

            return string.IsNullOrWhiteSpace(info) ? null : info;
        }
    }
}
