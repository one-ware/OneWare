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
    private readonly IMainDockService _mainDockService;

    private readonly INotifyCollectionChanged? _testBenchCollection;

    private bool _isVisible;

    private IFpgaSimulator? _selectedSimulator;

    private TestBenchContext? _testBenchContext;

    public UniversalFpgaProjectTestBenchToolBarViewModel(IFile file, IMainDockService mainDockService,
        FpgaService fpgaService)
    {
        File = file;
        _mainDockService = mainDockService;
        Simulators = fpgaService.Simulators;

        if (file is FpgaProjectFile { Root: UniversalFpgaProjectRoot fpgaProjectRoot } fpgaProjectFile)
        {
            IsVisible = fpgaProjectRoot.TestBenches.Contains(fpgaProjectFile);

            _testBenchCollection = fpgaProjectRoot.TestBenches;
            _testBenchCollection.CollectionChanged += OnCollectionChanged;
        }
    }

    public IFile File { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            SetProperty(ref _isVisible, value);
            if (value && TestBenchContext == null) _ = LoadContextAsync();
        }
    }

    public TestBenchContext? TestBenchContext
    {
        get => _testBenchContext;
        set => SetProperty(ref _testBenchContext, value);
    }

    public ObservableCollection<IFpgaSimulator> Simulators { get; }

    public IFpgaSimulator? SelectedSimulator
    {
        get => _selectedSimulator;
        set
        {
            SetProperty(ref _selectedSimulator, value);

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

    private void OnCollectionChanged(object? o, NotifyCollectionChangedEventArgs args)
    {
        if (File is FpgaProjectFile { Root: UniversalFpgaProjectRoot fpgaProjectRoot } fpgaProjectFile)
            IsVisible = fpgaProjectRoot.TestBenches.Contains(fpgaProjectFile);
    }

    public void Detach()
    {
        if (_testBenchCollection != null) _testBenchCollection.CollectionChanged -= OnCollectionChanged;
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