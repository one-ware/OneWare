using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.FolderProjectSystem.Models;

namespace OneWare.FolderProjectSystem;

public class FolderProjectManager : IProjectManager
{
    public FolderProjectManager()
    {
    }

    public string Extension => "";

    public Task<IProjectRoot?> LoadProjectAsync(string path)
    {
        if (!Directory.Exists(path)) return Task.FromResult<IProjectRoot?>(null);

        var root = new FolderProjectRoot(path);

        return Task.FromResult<IProjectRoot?>(root);
    }

    public Task<bool> SaveProjectAsync(IProjectRoot root)
    {
        return Task.FromResult(true);
    }

    public static void LoadFolder(IProjectFolder folder)
    {
        var matches = Directory.EnumerateFileSystemEntries(folder.FullPath);

        foreach (var match in matches)
        {
            var relativePath = Path.GetRelativePath(folder.FullPath, match);
            var attributes = File.GetAttributes(match);
            if (attributes.HasFlag(FileAttributes.Hidden)) continue;
            if (attributes.HasFlag(FileAttributes.Directory))
                folder.AddFolder(relativePath);
            else
                folder.AddFile(relativePath);
        }
    }

    public IEnumerable<MenuItemViewModel> ConstructContextMenu(IProjectEntry entry)
    {
        return Array.Empty<MenuItemViewModel>();
    }
}