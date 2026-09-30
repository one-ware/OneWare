using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;

namespace OneWare.ErrorList.ViewModels;

/// <summary>
///     Row in the problems tree: either a file group or a single problem.
/// </summary>
public abstract class ErrorListNode : ObservableObject
{
    private bool _isExpanded = true;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public virtual IEnumerable<ErrorListNode>? Children => null;
    public virtual bool HasChildren => false;
    public virtual string? Code => null;
    public virtual string? Source => null;
    public virtual string? Location => null;
}

public sealed class ErrorListFileNode : ErrorListNode
{
    private int _errorCount;
    private int _hintCount;
    private int _warningCount;

    public ErrorListFileNode(string filePath, IProjectRoot? root, IconModel? icon)
    {
        FilePath = filePath;
        Root = root;
        Icon = icon;
        FileName = Path.GetFileName(filePath);
        Directory = GetDisplayDirectory(filePath, root);
    }

    public string FilePath { get; }
    public string FileName { get; }

    /// <summary>
    ///     Folder shown next to the file name, relative to its project if it has one.
    /// </summary>
    public string Directory { get; }

    public IProjectRoot? Root { get; }
    public IconModel? Icon { get; }

    internal BatchObservableCollection<ErrorListProblemNode> ProblemNodes { get; } = new();

    public IReadOnlyList<ErrorListProblemNode> Problems => ProblemNodes;

    public override IEnumerable<ErrorListNode> Children => ProblemNodes;
    public override bool HasChildren => true;

    public int ErrorCount
    {
        get => _errorCount;
        private set
        {
            if (SetProperty(ref _errorCount, value)) OnPropertyChanged(nameof(HasErrors));
        }
    }

    public int WarningCount
    {
        get => _warningCount;
        private set
        {
            if (SetProperty(ref _warningCount, value)) OnPropertyChanged(nameof(HasWarnings));
        }
    }

    public int HintCount
    {
        get => _hintCount;
        private set
        {
            if (SetProperty(ref _hintCount, value)) OnPropertyChanged(nameof(HasHints));
        }
    }

    public bool HasErrors => ErrorCount > 0;
    public bool HasWarnings => WarningCount > 0;
    public bool HasHints => HintCount > 0;

    internal void UpdateCounts()
    {
        ErrorCount = Problems.Count(x => x.Item.Type == ErrorType.Error);
        WarningCount = Problems.Count(x => x.Item.Type == ErrorType.Warning);
        HintCount = Problems.Count(x => x.Item.Type == ErrorType.Hint);
    }

    private static string GetDisplayDirectory(string filePath, IProjectRoot? root)
    {
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        if (root == null) return directory;

        var relative = Path.GetRelativePath(root.RootFolderPath, directory);
        if (relative == ".") return root.Name;
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)) return directory;
        return Path.Combine(root.Name, relative);
    }
}

public sealed class ErrorListProblemNode(ErrorListItem item, ErrorListFileNode file) : ErrorListNode
{
    public ErrorListItem Item { get; } = item;
    public ErrorListFileNode File { get; } = file;

    /// <summary>
    ///     First line of the description, the full text is shown in the tooltip.
    /// </summary>
    public string Summary { get; } = FirstLine(item.Description);

    public bool IsError => Item.Type == ErrorType.Error;
    public bool IsWarning => Item.Type == ErrorType.Warning;
    public bool IsHint => Item.Type == ErrorType.Hint;

    public override string? Code => Item.Code;
    public override string? Source => Item.Source;

    public override string Location =>
        Item.StartColumn is { } column ? $"{Item.StartLine}:{column}" : Item.StartLine.ToString();

    private static string FirstLine(string text)
    {
        var trimmed = text.Trim();
        var newLine = trimmed.IndexOfAny(['\r', '\n']);
        return newLine < 0 ? trimmed : trimmed[..newLine].TrimEnd() + " …";
    }
}
