#nullable enable

using AvaloniaEdit.Document;
using OneWare.Shared.Enums;

namespace OneWare.Shared.Models
{
    public class ErrorListItemModel : IEquatable<ErrorListItemModel>, IComparable<ErrorListItemModel>
    {
        public string Description { get; init; }
        public ErrorType Type { get; init; }
        public string? Source { get; init; }
        public IFile File { get; init; }
        
        public IProjectRoot? Root { get; }
        public int StartLine { get; init; }
        public int? StartColumn { get; init; }
        public int? EndLine { get; init; }
        public int? EndColumn { get; init; }
        public string? Code { get; init; }
        
        public ErrorListItemModel(string description, ErrorType type, IFile file, string? source, int startLine, int? startColumn = null, int? endLine = null, int? endColumn = null, string? code = null)
        {
            Description = description;
            Type = type;
            Source = source;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
            File = file;
            Code = code;
            Root = (file as IProjectFile)?.Root;
        }

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

        public bool Equals(ErrorListItemModel? model)
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

        public int CompareTo(ErrorListItemModel? other)
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
            return Equals(obj as ErrorListItemModel);
        }
    }
}