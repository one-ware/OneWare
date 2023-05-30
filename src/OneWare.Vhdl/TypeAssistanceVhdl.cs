using System.Reactive.Disposables;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.TextMate;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Services;
using OneWare.Vhdl.Indentation;
using Prism.Ioc;
using TextMateSharp.Grammars;

namespace OneWare.Vhdl
{
    internal class TypeAssistanceVhdl : TypeAssistanceLsp
    {
        public TypeAssistanceVhdl(IEditor editor, LanguageServiceVhdl ls) : base(editor, ls)
        {
            CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new VhdlIndentationStrategy(CodeBox.Options);
            FoldingStrategy = new BraceFoldingStrategy();
        }
        
        public override string LineCommentSequence => "--";

        public override void CodeUpdated()
        {
            base.CodeUpdated();
        }

        public override Task<List<CompletionData>> GetCustomCompletionItemsAsync()
        {
            var items = new List<CompletionData>();

            items.Add(new CompletionData("Test", "test", "t", null, 0, 0));
            
            return Task.FromResult(items);
        }

        public override void TypeAssistance(TextInputEventArgs e)
        {
            if (e.Text.Contains(';') && Service.IsLanguageServiceReady)
            {
                var line = CodeBox.Document.GetLineByOffset(CodeBox.CaretOffset).LineNumber;
                Format(line, line);
            }
        }
        
        public override async Task<string?> GetHoverInfoAsync(int offset)
        {
            if (!Service.IsLanguageServiceReady) return null;

            var pos = CodeBox.Document.GetLocation(offset);

            var error = ContainerLocator.Container.Resolve<IErrorService>().GetErrorsForFile(CurrentFile).OrderBy(x => x.Type)
                .FirstOrDefault(error => pos.Line >= error.StartLine && pos.Column >= error.StartColumn && pos.Line < error.EndLine || pos.Line == error.EndLine && pos.Column <= error.EndColumn);

            var info = "";
            
            if(error != null) info += error.Description + "\n";
            
            var hover = await Service.RequestHoverAsync(CurrentFile,
                new Position(pos.Line - 1, pos.Column - 1));
            if (hover != null && !IsClosed)
            {
                if (hover.Contents.HasMarkedStrings)
                    info += hover.Contents.MarkedStrings!.First().Value.Split('\n')[0]; //TODO what is this?
                if (hover.Contents.HasMarkupContent) info += hover.Contents.MarkupContent?.Value;
            }

            return string.IsNullOrWhiteSpace(info) ? null : info;
        }
    }
}