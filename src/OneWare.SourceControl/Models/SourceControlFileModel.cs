using LibGit2Sharp;
using OneWare.Essentials.Models;

namespace OneWare.SourceControl.Models;

public class SourceControlFileModel
{
    public SourceControlFileModel(string fullPath, StatusEntry change)
    {
        FullPath = fullPath;
        Status = change;
    }

    public string FullPath { get; }

    public StatusEntry Status { get; set; }

    public string Name => Path.GetFileName(FullPath);
}