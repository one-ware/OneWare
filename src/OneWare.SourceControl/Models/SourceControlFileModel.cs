using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LibGit2Sharp;
using OneWare.Essentials.Models;

namespace OneWare.SourceControl.Models;

public class SourceControlFileModel : ObservableObject
{
    public SourceControlFileModel(string fullPath, StatusEntry change)
    {
        FullPath = fullPath;
        Status = change;
    }

    public IconModel? FileIcon { get; set; }
    
    public string FullPath { get; }

    public StatusEntry Status
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string Name => Path.GetFileName(FullPath);

    /// <summary>
    /// Repository relative directory of the file, shown dimmed next to the file name.
    /// </summary>
    public string RelativeDirectory => Path.GetDirectoryName(Status.FilePath)?.Replace('\\', '/') ?? string.Empty;
}