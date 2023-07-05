using OneWare.FolderProjectSystem.Models;
using OneWare.Shared;
using OneWare.Shared.Services;

namespace OneWare.FolderProjectSystem;

public class FolderProjectManager : IProjectManager
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ILogger _logger;
    
    public FolderProjectManager(IProjectExplorerService projectExplorerService, ILogger logger)
    {
        _projectExplorerService = projectExplorerService;
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
            _projectExplorerService.ImportFolderRecursive(path, root);
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
}