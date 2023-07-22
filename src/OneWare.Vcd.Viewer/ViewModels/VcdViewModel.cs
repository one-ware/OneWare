using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using DynamicData;
using OneWare.Shared;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.Views;
using OneWare.Vcd.Parser;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Viewer.Models;
using OneWare.WaveFormViewer.Controls;
using OneWare.WaveFormViewer.Enums;
using OneWare.WaveFormViewer.Models;
using OneWare.WaveFormViewer.ViewModels;
using Prism.Ioc;

namespace OneWare.Vcd.Viewer.ViewModels;

public class VcdViewModel : ExtendedDocument
{
    private readonly IProjectExplorerService _projectExplorerService;

    private CancellationTokenSource? _cancellationTokenSource;
    
    private VcdFile? _vcdFile;
    public ObservableCollection<VcdScopeModel> Scopes { get; } = new();
    
    private VcdScopeModel? _selectedScope;
    public VcdScopeModel? SelectedScope
    {
        get => _selectedScope;
        set => SetProperty(ref _selectedScope, value);
    }

    private IVcdSignal? _selectedSignal;
    public IVcdSignal? SelectedSignal
    {
        get => _selectedSignal;
        set => SetProperty(ref _selectedSignal, value);
    }
    
    public WaveFormViewModel WaveFormViewer { get; } = new();

    public VcdViewModel(string fullPath, IProjectExplorerService projectExplorerService, IDockService dockService) : base(fullPath, projectExplorerService, dockService)
    {
        WaveFormViewer.ExtendSignals = true;
        
        _projectExplorerService = projectExplorerService;
        
        Title = $"Loading {Path.GetFileName(fullPath)}";
    }

    protected override void ChangeCurrentFile(IFile? oldFile)
    {
        Refresh();
    }

    public void Refresh()
    {
        WaveFormViewer.MarkerOffset = long.MaxValue;
        if (CurrentFile != null)
            _ = LoadAsync();
    }

    private async Task<bool> LoadAsync()
    {
        IsLoading = true;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        Scopes.Clear();
        
        try
        {
            var progress = new Progress<int>();

            var lastTime = await VcdParser.TryFindLastTime(FullPath);
            
            var context = VcdParser.ParseVcdDefinition(FullPath);
            _vcdFile = context.Item1;
            
            Scopes.AddRange(_vcdFile.Definition.Scopes.Where(x => x.Signals.Any() || x.Scopes.Any()).Select(x => new VcdScopeModel(x)));
            
            var currentSignals = WaveFormViewer.Signals.Select(x => x.Signal.Id).ToArray();
            WaveFormViewer.Signals.Clear();
            foreach (var signal in currentSignals)
            {
                if(_vcdFile.Definition.SignalRegister.TryGetValue(signal, out var vcdSignal))
                    AddSignal(vcdSignal);
            }
            if (lastTime.HasValue) WaveFormViewer.Max = lastTime.Value;

            progress.ProgressChanged += (o, i) =>
            {
                Title = $"{Path.GetFileName(FullPath)} {i}%";
                if (!lastTime.HasValue) WaveFormViewer.Max = context.Item1.Definition.ChangeTimes.Last();
                else
                {
                    foreach (var (_, signal) in _vcdFile.Definition.SignalRegister)
                    {
                        signal.Invalidate();
                        WaveFormViewer.LoadingMarkerOffset = _vcdFile.Definition.ChangeTimes.Last();
                    }
                }
            };

            await VcdParser.StartAndReportProgressAsync(context.Item2, context.Item1, progress, _cancellationTokenSource.Token);

            WaveFormViewer.LoadingMarkerOffset = long.MaxValue;
            Title = CurrentFile is ExternalFile ? $"[{CurrentFile.Header}]" : CurrentFile!.Header;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            LoadingFailed = false;
        }
        
        IsLoading = false;
        return true;
    }

    public void AddSignal(IVcdSignal signal)
    {
        WaveFormViewer.AddSignal(signal);
    }

    protected override void Reset()
    {
        _cancellationTokenSource?.Cancel();
        if (_vcdFile == null) return;

        //Manual cleanup because ViewModel is stuck somewhere
        //TODO Fix DOCK to allow normal GC collection
        foreach (var (_, signal) in _vcdFile.Definition.SignalRegister)
        {
            signal.Clear();
        }
        _vcdFile.Definition.ChangeTimes.Clear();
        _vcdFile.Definition.ChangeTimes.TrimExcess();
    }
}