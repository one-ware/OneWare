using AvaloniaEdit.Indentation.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
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
            if (hover != null && !IsClosed)
                if (hover.Contents.HasMarkupContent)
                    return "```cpp\n" + hover.Contents.MarkupContent?.Value.Replace("→", "->") + "\n```";
            return null;
        }
    }
}