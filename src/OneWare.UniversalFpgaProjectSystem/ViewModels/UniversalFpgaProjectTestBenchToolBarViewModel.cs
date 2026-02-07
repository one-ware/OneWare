using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.ProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Context;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectTestBenchToolBarViewModel : ObservableObject
{
    private readonly IMainDockService _mainDockService;

    public UniversalFpgaProjectTestBenchToolBarViewModel(IFile file, IMainDockService mainDockService,
        FpgaService fpgaService)
    {
        File = file;
        _mainDockService = mainDockService;
        Simulators = fpgaService.Simulators;

        if (file is FpgaProjectFile { Root: UniversalFpgaProjectRoot fpgaProjectRoot } fpgaProjectFile)
        {
            IsVisible = fpgaProjectRoot.IsTestBench(fpgaProjectFile.RelativePath);
            
            fpgaProjectRoot.ProjectPropertyChanged += OnProjectPropertyChanged;
        }
    }

    public IFile File { get; }

    public bool IsVisible
    {
        get;
        set
        {
            SetProperty(ref field, value);
            if (value && TestBenchContext == null) _ = LoadContextAsync();
        }
    }

    public TestBenchContext? TestBenchContext
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ObservableCollection<IFpgaSimulator> Simulators { get; }

    public IFpgaSimulator? SelectedSimulator
    {
        get;
        set
        {
            SetProperty(ref field, value);

            if (TestBenchContext != null)
            {
                if (value != null)
                    TestBenchContext.SetBenchProperty("Simulator", value.Name);
                else
                    TestBenchContext.RemoveBenchProperty("Simulator");

                _ = TestBenchContextManager.SaveContextAsync(TestBenchContext);
            }
        }
    }

    private void OnProjectPropertyChanged(object? o, ProjectPropertyChangedEventArgs args)
    {
        if (File is FpgaProjectFile { Root: UniversalFpgaProjectRoot fpgaProjectRoot } fpgaProjectFile)
            IsVisible = fpgaProjectRoot.IsTestBench(fpgaProjectFile.RelativePath);
    }

    public void Detach()
    {
        if (File is FpgaProjectFile { Root: UniversalFpgaProjectRoot fpgaProjectRoot } fpgaProjectFile)
        {
            fpgaProjectRoot.ProjectPropertyChanged -= OnProjectPropertyChanged;
        }
    }

    private async Task LoadContextAsync()
    {
        TestBenchContext = await TestBenchContextManager.LoadContextAsync(File);

        var simulator = TestBenchContext.GetBenchProperty("Simulator");

        SelectedSimulator = Simulators.FirstOrDefault(x => x.Name == simulator);
    }

    public async Task SimulateCurrentAsync()
    {
        if (SelectedSimulator == null) return;
        if (TestBenchContext == null) throw new NullReferenceException(nameof(TestBenchContext));
        if (_mainDockService.OpenFiles.TryGetValue(File, out var fileView))
            await fileView.SaveAsync();
        await TestBenchContextManager.SaveContextAsync(TestBenchContext);
        await SelectedSimulator.SimulateAsync(File);
    }
}