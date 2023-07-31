using OneWare.Shared;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Views;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem;

public class UniversalFpgaProjectManager : IProjectManager
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IWindowService _windowService;
    private readonly ILogger _logger;
    
    public UniversalFpgaProjectManager(IProjectExplorerService projectExplorerService, IWindowService windowService, ILogger logger)
    {
        _projectExplorerService = projectExplorerService;
        _windowService = windowService;
        _logger = logger;
    }

    public async Task NewProjectDialogAsync()
    {
        await _windowService.ShowDialogAsync(new UniversalFpgaProjectCreatorView()
        {
            DataContext = ContainerLocator.Container.Resolve<UniversalFpgaProjectCreatorViewModel>()
        });
    }
    
    public Task<IProjectRoot?> LoadProjectAsync(string path)
    {
        var root = UniversalFpgaProjectParser.Deserialize(path);
        if (root != null)
        {
            _projectExplorerService.ImportFolderRecursive(path, root);
        }
        return Task.FromResult<IProjectRoot?>(null);
    }

    public Task<bool> SaveProjectAsync(IProjectRoot root)
    {
        return Task.FromResult(root is UniversalFpgaProjectRoot uFpga && UniversalFpgaProjectParser.Serialize(uFpga));
    }
}