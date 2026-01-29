using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Text;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using TextDocument = AvaloniaEdit.Document.TextDocument;

namespace OneWare.Essentials.Models;

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

    public int CompareTo(ErrorListItem? other)
    {
        if (other == null) return -1;
        if (Type < other.Type) return -1;
        if (Type > other.Type) return 1;
        var stringComp = string.Compare(File.Name, other.File.Name, StringComparison.OrdinalIgnoreCase);
        if (stringComp < 0) return -1;
        if (StartLine < other.StartLine) return -1;
        if (Equals(other)) return 0;
        return 1;
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

    /// <summary>
    ///     Returns start and end offsets. If no end offset is specified, the whole line length is used
    /// </summary>
    public (int startOffset, int endOffset) GetOffset(TextDocument document)
    {
        return document.GetStartAndEndOffset(StartLine, StartColumn, EndLine, EndColumn);
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

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("Type=").Append(Type);
        builder.Append("; Description=").Append(Description);
        if (!string.IsNullOrWhiteSpace(Source))
        {
            builder.Append("; Source=").Append(Source);
        }

        if (!string.IsNullOrWhiteSpace(Code))
        {
            builder.Append("; Code=").Append(Code);
        }

        builder.Append("; StartLine=").Append(StartLine);
        builder.Append("; StartColumn=").Append(StartColumn?.ToString() ?? "null");
        builder.Append("; EndLine=").Append(EndLine?.ToString() ?? "null");
        builder.Append("; EndColumn=").Append(EndColumn?.ToString() ?? "null");

        builder.Append("; FileName=").Append(File.Name);
        builder.Append("; FilePath=").Append(File.FullPath);
        builder.Append("; FileExtension=").Append(File.Extension);

        if (Root != null)
        {
            builder.Append("; ProjectTypeId=").Append(Root.ProjectTypeId);
            builder.Append("; ProjectPath=").Append(Root.ProjectPath);
            builder.Append("; RootFolderPath=").Append(Root.RootFolderPath);
        }

        return builder.ToString();
    }
}
