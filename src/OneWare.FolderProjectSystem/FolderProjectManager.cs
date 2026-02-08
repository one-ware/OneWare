using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
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

    public async Task<IProjectRoot?> LoadProjectAsync(string path)
    {
        if (!Directory.Exists(path)) return null;

        var root = new FolderProjectRoot(path);

        await root.InitializeAsync();
        
        return root;
    }

    public Task ReloadProjectAsync(IProjectRoot project)
    {
        return Task.FromResult(true);
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