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

    public IEnumerable<MenuItemViewModel> ConstructContextMenu(IProjectEntry entry)
    {
        return Array.Empty<MenuItemViewModel>();
    }
}