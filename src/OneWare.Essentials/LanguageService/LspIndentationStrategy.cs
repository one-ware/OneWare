using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;
using IFile = OneWare.Essentials.Models.IFile;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using TextDocument = AvaloniaEdit.Document.TextDocument;

namespace OneWare.Essentials.LanguageService
{
    public class LspIndentationStrategy : IIndentationStrategy
    {
        private readonly IFile _file;
        private readonly LanguageServiceLsp _languageService;
        private string _indentationString = "\t";

        public LspIndentationStrategy(TextEditorOptions options, LanguageServiceLsp languageS, IFile file)
        {
            IndentationString = options.IndentationString;
            _languageService = languageS;
            _file = file;
        }

        /// <summary>
        ///     Gets/Sets the indentation string.
        /// </summary>
        public string IndentationString
        {
            get => _indentationString;
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentException("Indentation string must not be null or empty");
                _indentationString = value;
            }
        }

        public void IndentLine(TextDocument document, DocumentLine line)
        {
            _ = IndentLineAsync(document, line);
        }

        public void IndentLines(TextDocument document, int beginLine, int endLine)
        {
            _ = IndentLinesAsync(document, beginLine, endLine);
        }

        public async Task IndentAllAsync(TextDocument document, bool keepEmptyLines)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var formattingContainer = await _languageService.RequestFormattingAsync(_file.FullPath);
            if(formattingContainer is not null) 
                ApplyContainer(document, formattingContainer);
        }

        public async Task IndentLineAsync(TextDocument document, DocumentLine line)
        {
            var lineNr = line.LineNumber;
            var formattingContainer = await _languageService.RequestRangeFormattingAsync(_file.FullPath,
                new Range
                {
                    Start = new Position { Line = lineNr - 1, Character = 0 },
                    End = new Position { Line = lineNr - 1, Character = line.Length }
                });
            if(formattingContainer is not null)
                ApplyContainer(document, formattingContainer);
        }

        public async Task IndentLinesAsync(TextDocument document, int beginLine, int endLine)
        {
            if (beginLine == 1 && endLine == document.LineCount)
            {
                await IndentAllAsync(document, true);
            }
            else
            {
                if (endLine > document.LineCount) return;
                var eline = document.GetLineByNumber(endLine);

                var formattingContainer = await _languageService.RequestRangeFormattingAsync(_file.FullPath,
                    new Range
                    {
                        Start = new Position { Line = beginLine - 1, Character = 0 },
                        End = new Position { Line = endLine - 1, Character = eline.Length }
                    });
                
                if(formattingContainer is not null)
                    ApplyContainer(document, formattingContainer);
            }
        }

        public void ApplyContainer(TextDocument document, TextEditContainer formattingContainer)
        {
            try
            {
                foreach (var f in formattingContainer)
                {
                    var sOffset = document.GetOffset(f.Range.Start.Line + 1, f.Range.Start.Character + 1);
                    var eOffset = f.Range.End.Line + 1 > document.LineCount
                        ? document.TextLength
                        : document.GetOffset(f.Range.End.Line + 1, f.Range.End.Character + 1);
                    document.Replace(sOffset, eOffset - sOffset, f.NewText);
                }
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
        }
    }
}