using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.Views;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectToolBarViewModel : ObservableObject
{
    private readonly IWindowService _windowService;

    private bool _isVisible;

    private bool _longTermProgramming;

    private UniversalFpgaProjectRoot? _project;

    public UniversalFpgaProjectToolBarViewModel(IWindowService windowService,
        IProjectExplorerService projectExplorerService, ISettingsService settingsService, FpgaService fpgaService)
    {
        _windowService = windowService;
        ProjectExplorerService = projectExplorerService;
        FpgaService = fpgaService;

        DownloaderConfigurationExtension =
            windowService.GetUiExtensions("UniversalFpgaToolBar_DownloaderConfigurationExtension");

        settingsService.Bind("UniversalFpgaProjectSystem_LongTermProgramming",
            this.WhenValueChanged(x => x.LongTermProgramming)).Subscribe(x => LongTermProgramming = x);

        projectExplorerService
            .WhenValueChanged(x => x.ActiveProject)
            .Subscribe(x =>
            {
                Project = x as UniversalFpgaProjectRoot;
                IsVisible = x is UniversalFpgaProjectRoot;
            });
    }

    public FpgaService FpgaService { get; }
    public IProjectExplorerService ProjectExplorerService { get; }

    public bool LongTermProgramming
    {
        get => _longTermProgramming;
        set => SetProperty(ref _longTermProgramming, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public UniversalFpgaProjectRoot? Project
    {
        get => _project;
        set => SetProperty(ref _project, value);
    }

    public ObservableCollection<UiExtension> DownloaderConfigurationExtension { get; }

    public void ToggleProjectToolchain(IFpgaToolchain toolchain)
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
        {
            if (project.Toolchain != toolchain) project.Toolchain = toolchain;
            else project.Toolchain = null;
            _ = ProjectExplorerService.SaveProjectAsync(project);
        }
    }

    public void ToggleLongTermProgramming()
    {
        LongTermProgramming = !LongTermProgramming;
    }

    public void ToggleProjectLoader(IFpgaLoader loader)
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
        {
            if (project.Loader != loader) project.Loader = loader;
            else project.Loader = null;
            _ = ProjectExplorerService.SaveProjectAsync(project);
        }
    }

    public async Task CompileAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
        {
            var name = project.Properties["Fpga"]?.ToString();
            if (name == null) {
                await OpenPinPlannerAsync();
                return;
            }
            
            var firstOrDefault = FpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == name);
            if (firstOrDefault == null) {
                await OpenPinPlannerAsync();
                return;
            }

            await project.RunToolchainAsync(new FpgaModel(firstOrDefault.LoadFpga()));
        }
    }
    
    public async Task OpenPinPlannerAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
            await _windowService.ShowDialogAsync(new UniversalFpgaProjectCompileView
            {
                DataContext =
                    ContainerLocator.Container.Resolve<UniversalFpgaProjectCompileViewModel>((
                        typeof(UniversalFpgaProjectRoot), project))
            });
    }

    public async Task OpenProjectSettingsAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
            await _windowService.ShowDialogAsync(new UniversalFpgaProjectSettingsEditorView()
            {
                DataContext =
                    ContainerLocator.Container.Resolve<UniversalFpgaProjectSettingsEditorViewModel>((
                        typeof(UniversalFpgaProjectRoot), project))
            });
    }
    
    public async Task DownloadAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot { Loader: not null } project)
            await project.Loader.DownloadAsync(project);
    }
}