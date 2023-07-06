using OneWare.Shared;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem;

public class UniversalFpgaProjectManager : IProjectManager
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ILogger _logger;
    
    public UniversalFpgaProjectManager(IProjectExplorerService projectExplorerService, ILogger logger)
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
        
        var root = new UniversalFpgaProjectRoot(path);
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