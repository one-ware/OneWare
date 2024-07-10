using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Context;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectTestBenchToolBarViewModel : ObservableObject
{
    private readonly IDockService _dockService;
    public IFile File { get; }
    
    private bool _isVisible = false;
    
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            SetProperty(ref _isVisible, value);
            if (value && TestBenchContext == null) _ = LoadContextAsync();
        }
    }

    private TestBenchContext? _testBenchContext;

    public TestBenchContext? TestBenchContext
    {
        get => _testBenchContext;
        set => SetProperty(ref _testBenchContext, value);
    }
    
    public ObservableCollection<IFpgaSimulator> Simulators { get; }

    private IFpgaSimulator? _selectedSimulator;
    
    public IFpgaSimulator? SelectedSimulator
    {
        get => _selectedSimulator;
        set
        {
            SetProperty(ref _selectedSimulator, value);

            if (TestBenchContext != null)
            {
                if(value != null)
                    TestBenchContext.SetBenchProperty("Simulator", value.Name);
                else 
                    TestBenchContext.RemoveBenchProperty("Simulator");
            
                _ = TestBenchContextManager.SaveContextAsync(TestBenchContext);
            }
        }
    }

    private readonly INotifyCollectionChanged? _testBenchCollection;
    
    public UniversalFpgaProjectTestBenchToolBarViewModel(IFile file, IDockService dockService, FpgaService fpgaService)
    {
        File = file;
        _dockService = dockService;
        Simulators = fpgaService.Simulators;
        
        if(file is FpgaProjectFile {Root: UniversalFpgaProjectRoot fpgaProjectRoot } fpgaProjectFile)
        {
            IsVisible = fpgaProjectRoot.TestBenches.Contains(fpgaProjectFile);

            _testBenchCollection = fpgaProjectRoot.TestBenches;
            _testBenchCollection.CollectionChanged += OnCollectionChanged;
        }
    }

    private void OnCollectionChanged(object? o, NotifyCollectionChangedEventArgs args)
    {
        if (File is FpgaProjectFile { Root: UniversalFpgaProjectRoot fpgaProjectRoot } fpgaProjectFile)
        {
            IsVisible = fpgaProjectRoot.TestBenches.Contains(fpgaProjectFile);
        }
    }
    
    public void Detach()
    {
        if(_testBenchCollection != null) _testBenchCollection.CollectionChanged -= OnCollectionChanged;
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
        if(_dockService.OpenFiles.TryGetValue(File, out var fileView))
            await fileView.SaveAsync();
        await TestBenchContextManager.SaveContextAsync(TestBenchContext);
        await SelectedSimulator.SimulateAsync(File);
    }
}