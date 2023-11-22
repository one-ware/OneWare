using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;
using OneWare.Vcd.Parser;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Viewer.Context;
using OneWare.Vcd.Viewer.Models;
using OneWare.WaveFormViewer.ViewModels;
using Prism.Ioc;

namespace OneWare.Vcd.Viewer.ViewModels;

public class VcdViewModel : ExtendedDocument, IStreamableDocument
{
    private readonly ISettingsService _settingsService;

    private bool _waitForLiveStream;
    private bool _isLiveExecution;
    
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

    public override string CloseWarningMessage => $"Do you want to save the current view to the file {CurrentFile?.Header}? All added signals will be opened automatically next time!";

    public VcdViewModel(string fullPath, IProjectExplorerService projectExplorerService, IDockService dockService, ISettingsService settingsService, IWindowService windowService) 
        : base(fullPath, projectExplorerService, dockService, windowService)
    {
        WaveFormViewer.ExtendSignals = true;
        
        _settingsService = settingsService;
        
        Title = $"Loading {Path.GetFileName(fullPath)}";

        settingsService.GetSettingObservable<int>("VcdViewer_LoadingThreads").Subscribe(x =>
        {
            LoadingThreads = x;
        });
        settingsService.Bind("VcdViewer_LoadingThreads", 
            this.WhenValueChanged(x => x.LoadingThreads));

        LoadingThreadOptions = settingsService.GetComboOptions<int>("VcdViewer_LoadingThreads");

        WaveFormViewer.SignalRemoved += (_, _) =>
        {
            if(_settingsService.GetSettingValue<bool>("VcdViewer_SaveView_Enable")) IsDirty = true;
        };
    }

    private static string GetSaveFilePath(string vcdPath)
    {
        return Path.Combine(Path.GetDirectoryName(vcdPath) ?? "", Path.GetFileNameWithoutExtension(vcdPath) + ".vcdconf");
    }
    
    public void PrepareLiveStream()
    {
        Reset();
        _waitForLiveStream = true;
        _isLiveExecution = false;
    }

    protected override void UpdateCurrentFile(IFile? oldFile)
    {
        Refresh();
    }
    
    public void Refresh()
    {
        WaveFormViewer.MarkerOffset = long.MaxValue;
        if (CurrentFile != null && !_isLiveExecution)
        {
            if (_waitForLiveStream)
            {
                _isLiveExecution = true;
                _waitForLiveStream = false;
            }
            _ = LoadAsync();
        }
    }

    private async Task<bool> LoadAsync()
    {
        IsLoading = true;
        if(_cancellationTokenSource != null) await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource = new CancellationTokenSource();
        Scopes.Clear();

        IDisposable? timer = null;

        try
        {
            var context = !WaveFormViewer.Signals.Any() ? await VcdContextManager.LoadContextAsync(GetSaveFilePath(FullPath)) : null;

            if (_cancellationTokenSource.IsCancellationRequested) return false;
            
            _vcdFile = VcdParser.ParseVcdDefinition(FullPath);
            Scopes.AddRange(_vcdFile.Definition.Scopes.Where(x => x.Signals.Count != 0 || x.Scopes.Count != 0)
                .Select(x => new VcdScopeModel(x)));
            
            var lastTime = !_isLiveExecution ? await VcdParser.TryFindLastTime(FullPath) : 0;
            
            if (_cancellationTokenSource.IsCancellationRequested) return false;

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

            //Check if a process is writing to this VCD to disable multicore parsing
            var useThreads = _isLiveExecution ? 1 : _loadingThreads;
            
            //Progress Handling
            var progressParts = new int[LoadingThreads];
            var progress = new Progress<(int part, int progress)>(x =>
            {
                if(x.part < progressParts.Length) progressParts[x.part] = x.progress;
            });

            timer = DispatcherTimer.Run(() =>
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) return false;
                
                var progressAverage = (int)progressParts.Average();
                ReportProgress(progressAverage, _isLiveExecution);
                return true;
            }, TimeSpan.FromMilliseconds(100), DispatcherPriority.MaxValue);
            
            await VcdParser.ReadSignalsAsync(FullPath, _vcdFile, progress, _cancellationTokenSource.Token, useThreads);
            
            if(_vcdFile != null)
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
        
        _isLiveExecution = false;
        IsLoading = false;
        IsDirty = false;
        return true;
    }

    private void ReportProgress(int progress, bool isLive)
    {
        if (_vcdFile == null) return;

        Title = isLive ? $"{Path.GetFileName(FullPath)} - LIVE" : $"{Path.GetFileName(FullPath)} {progress}%";
        
        if(_vcdFile.Definition.ChangeTimes.Any())
        {
            if (_vcdFile.Definition.ChangeTimes.LastOrDefault() > WaveFormViewer.Max) 
                WaveFormViewer.Max = _vcdFile.Definition.ChangeTimes.Last();
            
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
        if(_settingsService.GetSettingValue<bool>("VcdViewer_SaveView_Enable")) IsDirty = true;
    }

    protected override void Reset()
    {
        _cancellationTokenSource?.Cancel();
        
        //Manual cleanup because ViewModel is stuck somewhere
        _vcdFile = null;
    }

    public override async Task<bool> SaveAsync()
    {
        if(!_settingsService.GetSettingValue<bool>("VcdViewer_SaveView_Enable")) return true;
        var result = await VcdContextManager.SaveContextAsync(GetSaveFilePath(FullPath), new VcdContext(WaveFormViewer.Signals.Select(x => x.Signal.Id)));
        if (result) IsDirty = false;
        return result;
    }
}