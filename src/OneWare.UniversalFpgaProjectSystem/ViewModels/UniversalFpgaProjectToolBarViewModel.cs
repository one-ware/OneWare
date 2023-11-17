using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.Views;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectToolBarViewModel : ObservableObject
{
    public FpgaService FpgaService { get; }
    public IProjectExplorerService ProjectExplorerService { get; }
    private readonly IWindowService _windowService;
    
    private bool _longTermProgramming;

    public bool LongTermProgramming
    {
        get => _longTermProgramming;
        set => SetProperty(ref _longTermProgramming, value);
    }

    public UniversalFpgaProjectToolBarViewModel(IWindowService windowService, IProjectExplorerService projectExplorerService, FpgaService fpgaService)
    {
        _windowService = windowService;
        ProjectExplorerService = projectExplorerService;
        FpgaService = fpgaService;
    }

    public void ToggleProjectToolchain(IFpgaToolchain toolchain)
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
        {
            if (project.Toolchain != toolchain) project.Toolchain = toolchain;
            else project.Toolchain = null;
        }
    }
    
    public async Task CompileAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
        {
            await _windowService.ShowDialogAsync(new UniversalFpgaProjectCompileView()
            {
                DataContext = ContainerLocator.Container.Resolve<UniversalFpgaProjectCompileViewModel>((typeof(UniversalFpgaProjectRoot), project))
            });
        }
    }

    public async Task DownloadAsync()
    {
        await Task.Delay(100);
    }
}