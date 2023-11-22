﻿using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.SDK.Services;
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
            _ = ProjectExplorerService.SaveProjectAsync(project);
        }
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
            await _windowService.ShowDialogAsync(new UniversalFpgaProjectCompileView()
            {
                DataContext = ContainerLocator.Container.Resolve<UniversalFpgaProjectCompileViewModel>((typeof(UniversalFpgaProjectRoot), project))
            });
        }
    }

    public async Task DownloadAsync()
    {
        if (ProjectExplorerService.ActiveProject is UniversalFpgaProjectRoot { Loader: not null } project) 
            await project.Loader.DownloadAsync(project);
    }
}