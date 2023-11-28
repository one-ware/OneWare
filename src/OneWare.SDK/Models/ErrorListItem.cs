using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.SDK.Enums;
using IFile = OneWare.SDK.Models.IFile;
using TextDocument = AvaloniaEdit.Document.TextDocument;

namespace OneWare.SDK.Models
{
    public class ErrorListItem(
        string description,
        ErrorType type,
        IFile file,
        string? source,
        int startLine,
        int? startColumn = null,
        int? endLine = null,
        int? endColumn = null,
        string? code = null,
        Diagnostic? diagnostic = null)
        : IEquatable<ErrorListItem>, IComparable<ErrorListItem>
    {
        public Diagnostic? Diagnostic { get; set; } = diagnostic;
        public string Description { get; init; } = description;
        public ErrorType Type { get; init; } = type;
        public string? Source { get; init; } = source;
        public IFile File { get; init; } = file;
        public IProjectRoot? Root { get; } = (file as IProjectFile)?.Root;
        public int StartLine { get; init; } = startLine;
        public int? StartColumn { get; init; } = startColumn;
        public int? EndLine { get; init; } = endLine;
        public int? EndColumn { get; init; } = endColumn;
        public string? Code { get; init; } = code;

        /// <summary>
        /// Returns start and end offsets. If no end offset is specified, the whole line length is used
        /// </summary>
        public (int startOffset, int endOffset) GetOffset(TextDocument document)
        {
            if (StartLine <= 0) return (1, 1);
            if (StartLine > document.LineCount) return (document.TextLength, document.TextLength);
            
            var startOffset = document.GetOffset(StartLine, StartColumn ?? 0);

            if (EndLine != null && EndColumn != null)
            {
                var endOffset = document.GetOffset(EndLine.Value, EndColumn.Value);
                return (startOffset, endOffset);
            }

            var lineLength = document.GetLineByNumber(StartLine).Length;
            return (startOffset, startOffset + lineLength);
        }

        public bool Equals(ErrorListItem? model)
        {
            if (model == null) return false;
            return model.Description == Description
                   && model.Source == Source
                   && model.Type == Type
                   && model.File == File
                   && model.StartLine == StartLine
                   && model.StartColumn == StartColumn
                   && model.EndLine == EndLine
                   && model.EndColumn == EndColumn
                   && model.Code == Code;
        }

        public int CompareTo(ErrorListItem? other)
        {
            if(other == null) return -1;
            if (Type < other.Type) return -1;
            if(Type > other.Type) return 1;
            var stringComp = string.Compare(File.Header, other.File.Header, StringComparison.OrdinalIgnoreCase);
            if (stringComp < 0) return -1;
            if (StartLine < other.StartLine) return -1;
            if (Equals(other)) return 0;
            return 1;
        }
        
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Description);
            hash.Add(Source);
            hash.Add(Type);
            hash.Add(File);
            hash.Add(StartLine);
            hash.Add(StartColumn);
            hash.Add(EndLine);
            hash.Add(EndColumn);
            hash.Add(Code);
            return hash.ToHashCode();
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ErrorListItem);
        }
    }
}