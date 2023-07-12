using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Indentation.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Cpp
{
    internal class TypeAssistanceCpp : TypeAssistanceLsp
    {
        public override bool CanAddBreakPoints => true;

        public TypeAssistanceCpp(IEditor editor, LanguageServiceCpp ls) : base(editor, ls)
        {
            CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new CSharpIndentationStrategy(CodeBox.Options);
            FoldingStrategy = new FoldingStrategyCpp(); //new LspFoldingStrategy(ls, editor.CurrentFile);
        }
        
        public override async Task<string?> GetHoverInfoAsync(int offset)
        {
            if (!Service.IsLanguageServiceReady) return null;

            var pos = CodeBox.Document.GetLocation(offset);

            var error = ContainerLocator.Container.Resolve<IErrorService>().GetErrorsForFile(Editor.CurrentFile).OrderBy(x => x.Type)
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


        protected override ICompletionData ConvertCompletionItem(CompletionItem comp, int offset)
        {
            var icon = TypeAssistanceIconStore.Instance.Icons.TryGetValue(comp.Kind, out var instanceIcon)
                ? instanceIcon
                : TypeAssistanceIconStore.Instance.CustomIcons["Default"];

            var newLabel = comp.Label.Length > 0 ? comp.Label.Remove(0, 1) : "";
            newLabel = newLabel.Split("(")[0].Split("<")[0];

            void AfterComplete()
            {
                _ = ShowOverloadProviderAsync();
            }

            var description = comp.Documentation != null ? (comp.Documentation.MarkupContent != null ? comp.Documentation.MarkupContent.Value : comp.Documentation.String) : null;

            description = description?.Replace("\n", "\n\n");
            return new CompletionData(comp.InsertText ?? "", newLabel, description, icon, 0,
                comp, offset, AfterComplete);
        }

        protected override bool CharAtNormalCompletion(char c)
        {
            return char.IsLetterOrDigit(c) || c is '_' || c is ':';
        }
    }
}