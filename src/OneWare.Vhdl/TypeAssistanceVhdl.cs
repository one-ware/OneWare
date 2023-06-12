using System.Text.RegularExpressions;
using Avalonia.Input;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Services;
using OneWare.Vhdl.Indentation;
using Prism.Ioc;

namespace OneWare.Vhdl
{
    internal class TypeAssistanceVhdl : TypeAssistanceLsp
    {
        private readonly Regex _usedWordsRegex = new(@"\w{3,}");
        
        public TypeAssistanceVhdl(IEditor editor, LanguageServiceVhdl ls) : base(editor, ls)
        {
            CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new VhdlIndentationStrategy(CodeBox.Options);
            FoldingStrategy = new FoldingStrategyVhdl();
        }
        
        public override string LineCommentSequence => "--";
        

        public override async Task<List<CompletionData>> GetCustomCompletionItemsAsync()
        {
            var items = new List<CompletionData>();

            var text = Editor.CurrentDocument.Text;
            var usedWords = await Task.Run(() => _usedWordsRegex.Matches(text));

            items.Add(new CompletionData("library IEEE;\nuse IEEE.std_logic_1164.all;\nuse IEEE.numeric_std.all; ",
                "ieee", "IEEE Standard Packages",
                TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Reference], 0, CodeBox.CaretOffset));
            
            items.Add(new CompletionData(
                "entity " + Path.GetFileNameWithoutExtension(Editor.CurrentFile.Header) +
                " is\n    port(\n        [I/Os]$0\n    );\nend entity " +
                Path.GetFileNameWithoutExtension(Editor.CurrentFile.Header) + ";", "entity", "Entity Declaration",
                TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Class], 0, CodeBox.CaretOffset));
            
            foreach (var word in usedWords)
            {
                if(word.ToString() is { } s)
                    items.Add(new CompletionData(s, s, "Used word in document", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Snippet], 0, CodeBox.CaretOffset));
            }

            return items;
        }

        public override void TypeAssistance(TextInputEventArgs e)
        {
            if ((e.Text?.Contains(';') ?? false) && Service.IsLanguageServiceReady)
            {
                var line = CodeBox.Document.GetLineByOffset(CodeBox.CaretOffset).LineNumber;
                Format(line, line);
            }
        }
        
        public override async Task<string?> GetHoverInfoAsync(int offset)
        {
            if (!Service.IsLanguageServiceReady) return null;

            var pos = CodeBox.Document.GetLocation(offset);

            var error = ContainerLocator.Container.Resolve<IErrorService>().GetErrorsForFile(Editor.CurrentFile).OrderBy(x => x.Type)
                .FirstOrDefault(error => pos.Line >= error.StartLine && pos.Column >= error.StartColumn && pos.Line < error.EndLine || pos.Line == error.EndLine && pos.Column <= error.EndColumn);

            var info = "";
            
            if(error != null) info += error.Description + "\n";
            
            var hover = await Service.RequestHoverAsync(CurrentFile.FullPath,
                new Position(pos.Line - 1, pos.Column - 1));
            if (hover != null)
            {
                if (hover.Contents.HasMarkedStrings)
                    info += hover.Contents.MarkedStrings!.First().Value.Split('\n')[0]; //TODO what is this?
                if (hover.Contents.HasMarkupContent) info += hover.Contents.MarkupContent?.Value;
            }

            return string.IsNullOrWhiteSpace(info) ? null : info;
        }
    }
}