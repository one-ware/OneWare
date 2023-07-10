using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Indentation.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.LanguageService;

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

            if (!Service.IsLanguageServiceReady) return null;

            var location = CodeBox.Document.GetLocation(offset);

            var hover = await Service.RequestHoverAsync(Editor.CurrentFile.FullPath,
                new Position(location.Line - 1, location.Column - 1));
            if (hover != null)
                if (hover.Contents.HasMarkupContent)
                    return "```cpp\n" + hover.Contents.MarkupContent?.Value.Replace("→", "->") + "\n```";
            return null;
        }
        
        public override ICompletionData ConvertCompletionItem(CompletionItem comp, int offset)
        {
            var icon = TypeAssistanceIconStore.Instance.Icons.TryGetValue(comp.Kind, out var instanceIcon)
                ? instanceIcon
                : TypeAssistanceIconStore.Instance.CustomIcons["Default"];

            var newlabel = comp.Label.Length > 0 ? comp.Label.Remove(0, 1) : "";
            newlabel = newlabel.Split("(")[0].Split("<")[0];

            Action afterComplete = () => { _ = ShowOverloadProviderAsync(); };
            
            if (comp.InsertTextFormat == InsertTextFormat.PlainText)
                return new CompletionData(comp?.InsertText ?? "", newlabel, comp?.Documentation?.String, icon, 0,
                    comp, offset, afterComplete);
            return new CompletionData(comp?.InsertText ?? "", newlabel, comp?.Documentation?.String, icon, 0,
                comp, offset, afterComplete);
        }
    }
}