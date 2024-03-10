using OneWare.FolderProjectSystem.Models;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.FolderProjectSystem;

public class FolderProjectManager : IProjectManager
{
    private readonly ILogger _logger;

    public FolderProjectManager(ILogger logger)
    {
        _logger = logger;
    }

    public Task<IProjectRoot?> LoadProjectAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            return Task.FromResult<IProjectRoot?>(null);
        }
        
        var root = new FolderProjectRoot(path);

        return Task.FromResult<IProjectRoot?>(root);
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
            {
                folder.AddFolder(relativePath);
            }
            else
            {
                folder.AddFile(relativePath);
            }
        }
    }

    public Task<bool> SaveProjectAsync(IProjectRoot root)
    {
        return Task.FromResult(true);
    }

    public IEnumerable<MenuItemViewModel> ConstructContextMenu(IProjectEntry entry)
    {
        return Array.Empty<MenuItemViewModel>();
    }
}