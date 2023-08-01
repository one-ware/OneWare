using CommunityToolkit.Mvvm.Input;
using OneWare.Shared;
using OneWare.Shared.Models;
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
        return Task.FromResult<IProjectRoot?>(root);
    }

    public Task<bool> SaveProjectAsync(IProjectRoot root)
    {
        return Task.FromResult(root is UniversalFpgaProjectRoot uFpga && UniversalFpgaProjectParser.Serialize(uFpga));
    }

    public IEnumerable<MenuItemModel> ConstructContextMenu(IProjectEntry entry)
    {
        switch (entry)
        {
            case UniversalFpgaProjectRoot root:
                yield return new MenuItemModel("Save")
                {
                    Header = "Save",
                    Command = new AsyncRelayCommand(() => SaveProjectAsync(root)),
                };
                break;
        }
    }
}