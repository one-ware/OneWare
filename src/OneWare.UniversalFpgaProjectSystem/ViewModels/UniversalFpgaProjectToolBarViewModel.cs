﻿using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.Views;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectToolBarViewModel : ObservableObject
{
    private readonly IWindowService _windowService;
    private readonly ILogger _logger;
    private readonly Func<UniversalFpgaProjectRoot, UniversalFpgaProjectPinPlannerViewModel> _pinPlannerFactory;
    private readonly Func<UniversalFpgaProjectRoot, UniversalFpgaProjectSettingsEditorViewModel> _settingsEditorFactory;

    private bool _isVisible;
    private bool _longTermProgramming;
    private UniversalFpgaProjectRoot? _project;

    public UniversalFpgaProjectToolBarViewModel(
        IWindowService windowService,
        IProjectExplorerService projectExplorerService,
        ISettingsService settingsService,
        FpgaService fpgaService,
        ILogger logger,
        Func<UniversalFpgaProjectRoot, UniversalFpgaProjectPinPlannerViewModel> pinPlannerFactory,
        Func<UniversalFpgaProjectRoot, UniversalFpgaProjectSettingsEditorViewModel> settingsEditorFactory)
    {
        _windowService = windowService;
        _logger = logger;
        _pinPlannerFactory = pinPlannerFactory;
        _settingsEditorFactory = settingsEditorFactory;

        ProjectExplorerService = projectExplorerService;
        FpgaService = fpgaService;

        DownloaderConfigurationExtension =
            windowService.GetUiExtensions("UniversalFpgaToolBar_DownloaderConfigurationExtension");

        CompileMenuExtension = windowService.GetUiExtensions("UniversalFpgaToolBar_CompileMenuExtension");

        PinPlannerMenuExtension = windowService.GetUiExtensions("UniversalFpgaToolBar_PinPlannerMenuExtension");

        settingsService
            .Bind("UniversalFpgaProjectSystem_LongTermProgramming", this.WhenValueChanged(x => x.LongTermProgramming))
            .Subscribe(x => LongTermProgramming = x);

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
            _logger.Warning("No Active Project");
            return (null, null);
        }

        var name = project.Properties["Fpga"]?.ToString();
        var fpgaPackage = FpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == name);
        if (fpgaPackage == null)
        {
            _logger.Warning("No FPGA Selected, open Pin Planner first");
            return (project, null);
        }

        return (project, new FpgaModel(fpgaPackage.LoadFpga()));
    }

    public async Task CompileAsync()
    {
        if (EnsureProjectAndFpga() is not { project: not null, fpga: not null } data)
            return;

        await ProjectExplorerService.SaveOpenFilesForProjectAsync(data.project);
        await data.project.RunToolchainAsync(data.fpga);
    }

    public async Task OpenPinPlannerAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
        {
            await ProjectExplorerService.SaveOpenFilesForProjectAsync(project);
            var viewModel = _pinPlannerFactory(project);
            await _windowService.ShowDialogAsync(new UniversalFpgaProjectPinPlannerView
            {
                DataContext = viewModel
            });
        }
    }

    public async Task OpenProjectSettingsAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot project)
        {
            var viewModel = _settingsEditorFactory(project);
            await _windowService.ShowDialogAsync(new UniversalFpgaProjectSettingsEditorView
            {
                DataContext = viewModel
            });
        }
    }

    public async Task DownloadAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot { Loader: not null } project)
            await project.Loader.DownloadAsync(project);
    }
}
