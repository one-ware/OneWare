using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Indentation.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Cpp.Folding;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;

namespace OneWare.Cpp
{
    internal class TypeAssistanceCpp : TypeAssistanceLanguageService
    {
        public override bool CanAddBreakPoints => true;

        public TypeAssistanceCpp(IEditor editor, LanguageServiceCpp ls) : base(editor, ls)
        {
            CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new CSharpIndentationStrategy(CodeBox.Options);
            FoldingStrategy = new RegexFoldingStrategy(FoldingRegexCpp.FoldingStart, FoldingRegexCpp.FoldingEnd); //new LspFoldingStrategy(ls, editor.CurrentFile);
            LineCommentSequence = "//";
        }
        
        public override async Task<string?> GetHoverInfoAsync(int offset)
        {
            if (!Service.IsLanguageServiceReady) return null;

            var pos = CodeBox.Document.GetLocation(offset);

            var error = ContainerLocator.Container.Resolve<IErrorService>().GetErrorsForFile(Editor.CurrentFile!).OrderBy(x => x.Type)
                .FirstOrDefault(error => pos.Line >= error.StartLine 
                                         && pos.Line <= error.EndLine 
                                         && pos.Column >= error.StartColumn
                                         && pos.Column <= error.EndColumn);
            var info = "";
            
            if(error != null) info += error.Description + "\n";
            
            var hover = await Service.RequestHoverAsync(CurrentFile.FullPath,
                new Position(pos.Line - 1, pos.Column - 1));
            if (hover != null)
            {
                if (hover.Contents.HasMarkedStrings)
                    info += hover.Contents.MarkedStrings!.First().Value.Split('\n')[0]; //TODO what is this?
                if (hover.Contents.HasMarkupContent) info += $"```cpp\n{hover.Contents.MarkupContent?.Value}\n```";
            }

            return string.IsNullOrWhiteSpace(info) ? null : info;
        }
    }
}