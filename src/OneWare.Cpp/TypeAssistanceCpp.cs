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
            // if (MainDock.Debugger.IsDebugging && !MainDock.Debugger.Running)
            // {
            //     var word = CodeBox.GetWordAtOffset(offset);
            //     var result = MainDock.Debugger.EvaluateExpression(word);
            //     var val = result?.GetValue("value");
            //     if (!string.IsNullOrEmpty(val)) return "%object:" + word + "%" + val;
            // }

            var pos = CodeBox.Document.GetLocation(offset);
            
            var error = ContainerLocator.Container.Resolve<IErrorService>().GetErrorsForFile(Editor.CurrentFile).OrderBy(x => x.Type)
                .FirstOrDefault(error => pos.Line >= error.StartLine && pos.Column >= error.StartColumn && pos.Line < error.EndLine || pos.Line == error.EndLine && pos.Column <= error.EndColumn);

            var info = "";
            
            if(error != null) info += error.Description + "\n";
            
            if (!Service.IsLanguageServiceReady) return null;

            var location = CodeBox.Document.GetLocation(offset);

            var hover = await Service.RequestHoverAsync(Editor.CurrentFile.FullPath,
                new Position(location.Line - 1, location.Column - 1));
            if (hover != null)
                if (hover.Contents.HasMarkupContent)
                    info += "```cpp\n" + hover.Contents.MarkupContent?.Value.Replace("→", "->") + "\n```";
            return string.IsNullOrWhiteSpace(info) ? null : info;;
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
    }
}