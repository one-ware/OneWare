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
        
        CompileMenuExtension = windowService.GetUiExtensions("UniversalFpgaToolBar_CompileMenuExtension");
        
        PinPlannerMenuExtension = windowService.GetUiExtensions("UniversalFpgaToolBar_PinPlannerMenuExtension");

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

    public ObservableCollection<UiExtension> PinPlannerMenuExtension { get; }

    public ObservableCollection<UiExtension> CompileMenuExtension { get; }

    public ObservableCollection<UiExtension> DownloaderConfigurationExtension { get; }

    public void ToggleLongTermProgramming()
    {
        LongTermProgramming = !LongTermProgramming;
    }

    private (UniversalFpgaProjectRoot? project, FpgaModel? fpga) EnsureProjectAndFpga()
    {
        if (ProjectExplorerService.ActiveProject is not UniversalFpgaProjectRoot project)
        {
            ContainerLocator.Container.Resolve<ILogger>().Warning("No Active Project");
            return (null, null);
        }

        var name = project.Properties["Fpga"]?.ToString();
        var fpgaPackage = FpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == name);
        if (fpgaPackage == null)
        {
            ContainerLocator.Container.Resolve<ILogger>().Warning("No FPGA Selected, open Pin Planner first");
            return (project, null);
        }

        return (project, new FpgaModel(fpgaPackage.LoadFpga()));
    }
    
    public async Task CompileAsync()
    {
        if(EnsureProjectAndFpga() is not {project: not null, fpga: not null} data) return;
        
        await data.project.RunToolchainAsync(data.fpga);
    }
    
    public async Task SynthesisAsync()
    {
        if(EnsureProjectAndFpga() is not {project: { Toolchain: not null }, fpga: not null} data) return;

        await data.project.Toolchain.SynthesisAsync(data.project, data.fpga);
    }
    
    public async Task FitAsync()
    {
        if(EnsureProjectAndFpga() is not {project: { Toolchain: not null }, fpga: not null} data) return;

        await data.project.Toolchain.FitAsync(data.project, data.fpga);
    }
    
    public async Task AssembleAsync()
    {
        if(EnsureProjectAndFpga() is not {project: { Toolchain: not null }, fpga: not null} data) return;

        await data.project.Toolchain.AssembleAsync(data.project, data.fpga);
    }
    
    public async Task OpenPinPlannerAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
            await _windowService.ShowDialogAsync(new UniversalFpgaProjectPinPlannerView
            {
                DataContext =
                    ContainerLocator.Container.Resolve<UniversalFpgaProjectPinPlannerViewModel>((
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