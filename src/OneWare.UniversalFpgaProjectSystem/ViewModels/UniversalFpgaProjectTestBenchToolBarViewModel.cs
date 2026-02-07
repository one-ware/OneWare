using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Extensions;
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
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly string _filePath;
    private UniversalFpgaProjectRoot? _projectRoot;
    
    public UniversalFpgaProjectTestBenchToolBarViewModel(string filePath, IMainDockService mainDockService,
        FpgaService fpgaService, IProjectExplorerService projectExplorerService)
    {
        _filePath = filePath;
        _mainDockService = mainDockService;
        _projectExplorerService = projectExplorerService;
        Simulators = fpgaService.Simulators;

        var root = projectExplorerService.GetRootFromFile(filePath);
        
        if (root is UniversalFpgaProjectRoot fpgaProjectRoot)
        {
            _projectRoot = fpgaProjectRoot;
            IsVisible = fpgaProjectRoot.IsTestBench(Path.GetRelativePath(root.RootFolderPath, filePath));
            
            fpgaProjectRoot.ProjectPropertyChanged += OnProjectPropertyChanged;
        }
    }

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
        if (_projectRoot != null)
            IsVisible = _projectRoot.IsTestBench(Path.GetRelativePath(_projectRoot.RootFolderPath, _filePath));
    }

    public void Detach()
    {
        if (_projectRoot != null)
            _projectRoot.ProjectPropertyChanged -= OnProjectPropertyChanged;
    }

    private async Task LoadContextAsync()
    {
        TestBenchContext = await TestBenchContextManager.LoadContextAsync(_filePath);

        var simulator = TestBenchContext.GetBenchProperty("Simulator");

        SelectedSimulator = Simulators.FirstOrDefault(x => x.Name == simulator);
    }

    public async Task SimulateCurrentAsync()
    {
        if (SelectedSimulator == null) return;
        if (TestBenchContext == null) throw new NullReferenceException(nameof(TestBenchContext));
        if (_mainDockService.OpenFiles.TryGetValue(_filePath.ToPathKey(), out var fileView))
            await fileView.SaveAsync();
        await TestBenchContextManager.SaveContextAsync(TestBenchContext);
        await SelectedSimulator.SimulateAsync(_filePath);
    }
}
