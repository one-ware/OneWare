using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.FolderProjectSystem.Models;
using OneWare.ProjectSystem.Models;

namespace OneWare.FolderProjectSystem;

public class FolderProjectManager : IProjectManager
{
    private readonly ILogger _logger;

    public FolderProjectManager(ILogger logger)
    {
        _logger = logger;
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
        folder.Children.Clear();
        folder.Entities.Clear();
        
        var options = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false
        };
        var directoryMatches = Directory.EnumerateDirectories(folder.FullPath, "*", options);
        
        foreach (var match in directoryMatches)
        {
            var newFolder = new ProjectFolder(Path.GetFileName(match), folder);
            folder.Children.Add(newFolder);
            folder.Entities.Add(newFolder);
            (folder.Root as FolderProjectRoot)!.RegisterEntry(newFolder);
        }
        
        var fileMatches = Directory.EnumerateFiles(folder.FullPath, "*.*", options);
        
        foreach (var match in fileMatches)
        {
            var newFile = new ProjectFile(Path.GetFileName(match), folder);
            folder.Children.Add(newFile);
            folder.Entities.Add(newFile);
            (folder.Root as FolderProjectRoot)!.RegisterEntry(newFile);
        }
    }

    public IEnumerable<MenuItemViewModel> ConstructContextMenu(IProjectEntry entry)
    {
        return Array.Empty<MenuItemViewModel>();
    }
}