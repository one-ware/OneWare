using LibGit2Sharp;
using OneWare.Essentials.Models;

namespace OneWare.SourceControl.Models;

public class GitCommitFileModel
{
    public GitCommitFileModel(GitGraphRow owner, string path, string? oldPath, ChangeKind status, string fullPath)
    {
        Owner = owner;
        Path = path;
        OldPath = oldPath;
        Status = status;
        FullPath = fullPath;
    }

    public GitGraphRow Owner { get; }

    /// <summary>Repository relative path with forward slashes.</summary>
    public string Path { get; }

    public string? OldPath { get; }

    public ChangeKind Status { get; }

    public string FullPath { get; }

    public IconModel? FileIcon { get; init; }

    public string Name => System.IO.Path.GetFileName(Path);

    public string RelativeDirectory
    {
        get
        {
            var directory = System.IO.Path.GetDirectoryName(Path)?.Replace('\\', '/') ?? string.Empty;
            return OldPath != null && OldPath != Path ? $"{directory}  ← {OldPath}".Trim() : directory;
        }
    }
}
