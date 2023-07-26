using System.Collections.ObjectModel;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Shared;
using OneWare.Shared.Services;
using OneWare.Shared.Views;
using OneWare.Vcd.Parser;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Viewer.Context;
using OneWare.Vcd.Viewer.Models;
using OneWare.WaveFormViewer.ViewModels;
using Prism.Ioc;

namespace OneWare.Vcd.Viewer.ViewModels;

public class VcdViewModel : ExtendedDocument
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ISettingsService _settingsService;

    private bool _isParsing;
    
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

        WaveFormViewer.SignalRemoved += (_, _) =>
        {
            if(_settingsService.GetSettingValue<bool>("VcdViewer_SaveView_Enable")) IsDirty = true;
        };
    }

    protected override void ChangeCurrentFile(IFile? oldFile)
    {
        Refresh();
    }

    public void Refresh()
    {
        WaveFormViewer.MarkerOffset = long.MaxValue;
        if (CurrentFile != null && !_isParsing)
            _ = LoadAsync();
    }

    private async Task<bool> LoadAsync()
    {
        IsLoading = true;
        _isParsing = true;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        Scopes.Clear();
        
        IDisposable? timer = null;

        try
        {
            var context = _vcdFile == null ? await VcdContextManager.LoadContextAsync(FullPath + ".vcdSave.json") : null;
            
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

            var useThreads = FileInUse(FullPath) ? 1 : _loadingThreads;
            //Progress Handling
            var progressParts = new int[LoadingThreads];
            var progress = new Progress<(int part, int progress)>(x =>
            {
                if(x.part < progressParts.Length) progressParts[x.part] = x.progress;
            });

            timer = DispatcherTimer.Run(() =>
            {
                var progressAverage = (int)progressParts.Average();
                    ReportProgress(progressAverage, lastTime ?? 0, useThreads);
                    return true;
            }, TimeSpan.FromMilliseconds(100), DispatcherPriority.MaxValue);

            //Check if a process is writing to this VCD to disable multicore parsing

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

        _isParsing = false;
        IsLoading = false;
        IsDirty = false;
        return true;
    }
    
    private static bool FileInUse(string path) 
    {
        try
        {
            using var f = File.OpenWrite(path);
        }
        catch
        {
            return true;
        }

        return false;
    }

    private void ReportProgress(int progress, long lastTime, int threads)
    {
        if (_vcdFile == null) return;
        Title = $"{Path.GetFileName(FullPath)} {progress}%";
        
        if (_vcdFile.Definition.ChangeTimes.LastOrDefault() > lastTime) 
            WaveFormViewer.Max = _vcdFile.Definition.ChangeTimes.Last();
        
        if(_vcdFile.Definition.ChangeTimes.Any())
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
        if(_settingsService.GetSettingValue<bool>("VcdViewer_SaveView_Enable")) IsDirty = true;
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
        if(!_settingsService.GetSettingValue<bool>("VcdViewer_SaveView_Enable")) return true;
        var result = await VcdContextManager.SaveContextAsync(FullPath + ".vcdSave.json", new VcdContext(WaveFormViewer.Signals.Select(x => x.Signal.Id)));
        if (result) IsDirty = false;
        return result;
    }
}