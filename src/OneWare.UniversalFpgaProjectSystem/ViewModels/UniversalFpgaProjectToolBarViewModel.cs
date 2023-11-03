using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Views;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectToolBarViewModel : ObservableObject
{
    private readonly IWindowService _windowService;
    private readonly IProjectExplorerService _projectExplorerService;
    
    private bool _longTermProgramming;

    public bool LongTermProgramming
    {
        get => _longTermProgramming;
        set => SetProperty(ref _longTermProgramming, value);
    }

    public UniversalFpgaProjectToolBarViewModel(IWindowService windowService, IProjectExplorerService projectExplorerService)
    {
        _windowService = windowService;
        _projectExplorerService = projectExplorerService;
    }
    
    public async Task CompileAsync()
    {
        if (_projectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
        {
            await _windowService.ShowDialogAsync(new UniversalFpgaProjectCompileView()
            {
                DataContext = ContainerLocator.Container.Resolve<UniversalFpgaProjectCreatorViewModel>((typeof(UniversalFpgaProjectRoot), project))
            });
        }
    }

    public async Task DownloadAsync()
    {
        await Task.Delay(100);
    }
}