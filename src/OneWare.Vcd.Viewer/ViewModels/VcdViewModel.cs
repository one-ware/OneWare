using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using DynamicData;
using DynamicData.Binding;
using OneWare.Shared;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.Views;
using OneWare.Vcd.Parser;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Viewer.Context;
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
    private readonly ISettingsService _settingsService;

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

    public int[] LoadingThreadOptions { get; }
    
    private int _loadingThreads;
    public int LoadingThreads
    {
        get => _loadingThreads;
        set => SetProperty(ref _loadingThreads, value);
    }
    
    public WaveFormViewModel WaveFormViewer { get; } = new();

    public VcdViewModel(string fullPath, IProjectExplorerService projectExplorerService, IDockService dockService, ISettingsService settingsService, IWindowService windowService) 
        : base(fullPath, projectExplorerService, dockService, windowService)
    {
        WaveFormViewer.ExtendSignals = true;
        
        _projectExplorerService = projectExplorerService;
        _settingsService = settingsService;
        
        Title = $"Loading {Path.GetFileName(fullPath)}";

        settingsService.GetSettingObservable<int>("VcdViewer_LoadingThreads").Subscribe(x =>
        {
            LoadingThreads = x;
        });
        settingsService.Bind("VcdViewer_LoadingThreads", 
            this.WhenValueChanged(x => x.LoadingThreads));

        LoadingThreadOptions = settingsService.GetComboOptions<int>("VcdViewer_LoadingThreads");
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
        
        IDisposable? timer = null;

        try
        {
            var context = _vcdFile == null ? await VcdContextManager.LoadContextAsync(FullPath + "vcdSave.json") : null;
            
            _vcdFile = VcdParser.ParseVcdDefinition(FullPath);
            Scopes.AddRange(_vcdFile.Definition.Scopes.Where(x => x.Signals.Any() || x.Scopes.Any())
                .Select(x => new VcdScopeModel(x)));
            
            var lastTime = await VcdParser.TryFindLastTime(FullPath);

            if (context == null)
            {
                var currentSignals = WaveFormViewer.Signals.Select(x => x.Signal.Id).ToArray();
                WaveFormViewer.Signals.Clear();
                foreach (var signal in currentSignals)
                {
                    if (_vcdFile.Definition.SignalRegister.TryGetValue(signal, out var vcdSignal))
                        WaveFormViewer.AddSignal(vcdSignal);
                }
            }
            else
            {
                foreach (var signal in context.OpenIds)
                {
                    if (_vcdFile.Definition.SignalRegister.TryGetValue(signal, out var vcdSignal))
                        WaveFormViewer.AddSignal(vcdSignal);
                }
            }

            if (lastTime.HasValue) WaveFormViewer.Max = lastTime.Value;

            //Progress Handling
            var progressParts = new int[LoadingThreads];
            var progress = new Progress<(int part, int progress)>(x =>
            {
                if(x.part < progressParts.Length) progressParts[x.part] = x.progress;
            });

            timer = DispatcherTimer.Run(() =>
            {
                var progressAverage = (int)progressParts.Average();
                    ReportProgress(progressAverage, lastTime);
                    return progressAverage < 100;
                }, TimeSpan.FromMilliseconds(100), DispatcherPriority.MaxValue);

            await VcdParser.ReadSignalsAsync(FullPath, _vcdFile, progress, _cancellationTokenSource.Token,
                _loadingThreads);

            foreach (var (_, s) in _vcdFile.Definition.SignalRegister)
            {
                s.Invalidate();
            }

            WaveFormViewer.LoadingMarkerOffset = long.MaxValue;
            Title = CurrentFile is ExternalFile ? $"[{CurrentFile.Header}]" : CurrentFile!.Header;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            LoadingFailed = false;
        }
        timer?.Dispose();
        
        IsLoading = false;
        return true;
    }

    private void ReportProgress(int progress, long? lastTime)
    {
        if (_vcdFile == null) return;
        Title = $"{Path.GetFileName(FullPath)} {progress}%";
        if (!lastTime.HasValue) WaveFormViewer.Max = _vcdFile.Definition.ChangeTimes.Last();
        else if(LoadingThreads == 1 && _vcdFile.Definition.ChangeTimes.Any())
        {
            foreach (var (_, signal) in _vcdFile.Definition.SignalRegister)
            {
                signal.Invalidate();
                WaveFormViewer.LoadingMarkerOffset = _vcdFile.Definition.ChangeTimes.Last();
            }
        }
    }
    
    public void AddSignal(IVcdSignal signal)
    {
        WaveFormViewer.AddSignal(signal);
        IsDirty = true;
    }

    protected override void Reset()
    {
        _cancellationTokenSource?.Cancel();
        //Manual cleanup because ViewModel is stuck somewhere
        //TODO Fix DOCK to allow normal GC collection
        _vcdFile = null;
    }

    public override async Task<bool> SaveAsync()
    {
        var result = await VcdContextManager.SaveContextAsync(FullPath + ".vcdSave.json", new VcdContext(WaveFormViewer.Signals.Select(x => x.Signal.Id)));
        if (result) IsDirty = false;
        return result;
    }
}