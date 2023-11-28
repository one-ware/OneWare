using OneWare.FolderProjectSystem.Models;
using OneWare.SDK.Helpers;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;

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
        try
        {
            ProjectHelper.ImportEntries(path, root);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
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