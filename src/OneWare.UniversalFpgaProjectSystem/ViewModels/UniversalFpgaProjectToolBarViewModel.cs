using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.Views;

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

    public ObservableCollection<OneWareUiExtension> PinPlannerMenuExtension { get; }

    public ObservableCollection<OneWareUiExtension> CompileMenuExtension { get; }

    public ObservableCollection<OneWareUiExtension> DownloaderConfigurationExtension { get; }

    public void ToggleLongTermProgramming()
    {
        LongTermProgramming = !LongTermProgramming;
    }

    private UniversalFpgaProjectRoot? EnsureProject()
    {
        if (ProjectExplorerService.ActiveProject is not UniversalFpgaProjectRoot project)
        {
            ContainerLocator.Container.Resolve<ILogger>().Warning("No Active Project");
            return null;
        }

        return project;
    }

    public async Task CompileAsync()
    {
        if (EnsureProject() is not { } project) return;

        await ProjectExplorerService.SaveOpenFilesForProjectAsync(project);
        await FpgaService.RunToolchainAsync(project);
    }

    public async Task OpenPinPlannerAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
        {
            await ProjectExplorerService.SaveOpenFilesForProjectAsync(project);

            await _windowService.ShowDialogAsync(new UniversalFpgaProjectPinPlannerView
            {
                DataContext =
                    ContainerLocator.Container.Resolve<UniversalFpgaProjectPinPlannerViewModel>((
                        typeof(UniversalFpgaProjectRoot), project))
            });
        }
    }

    public async Task OpenProjectSettingsAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
            await _windowService.ShowDialogAsync(new UniversalFpgaProjectSettingsEditorView
            {
                DataContext =
                    ContainerLocator.Container.Resolve<UniversalFpgaProjectSettingsEditorViewModel>((
                        typeof(UniversalFpgaProjectRoot), project))
            });
    }

    public async Task DownloadAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot { Loader: not null } project)
        {
            var loader = FpgaService.Loaders.FirstOrDefault(x => x.Id == project.Loader);

            if (loader == null) return;
            
            await loader.DownloadAsync(project);
        }
    }
}
