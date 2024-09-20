using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Vcd.Parser;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Viewer.Context;
using OneWare.Vcd.Viewer.Models;
using OneWare.WaveFormViewer.Models;
using OneWare.WaveFormViewer.ViewModels;
using Prism.Ioc;

namespace OneWare.Vcd.Viewer.ViewModels;

public class VcdViewModel : ExtendedDocument, IStreamableDocument
{
    private readonly ISettingsService _settingsService;

    private CancellationTokenSource? _cancellationTokenSource;
    private CompositeDisposable _compositeDisposable = new();
    private bool _isLiveExecution;

    private VcdContext? _lastContext;

    private int _loadingThreads;

    private VcdScopeModel? _selectedScope;

    private IVcdSignal? _selectedSignal;
    private VcdFile? _vcdFile;

    private bool _waitForLiveStream;

    public VcdViewModel(string fullPath, IProjectExplorerService projectExplorerService, IDockService dockService,
        ISettingsService settingsService, IWindowService windowService)
        : base(fullPath, projectExplorerService, dockService, windowService)
    {
        WaveFormViewer.ExtendSignals = true;

        _settingsService = settingsService;

        Title = $"Loading {Path.GetFileName(fullPath)}";

        settingsService.GetSettingObservable<int>("VcdViewer_LoadingThreads").Subscribe(x => { LoadingThreads = x; });
        _ = settingsService.Bind("VcdViewer_LoadingThreads", this.WhenValueChanged(x => x.LoadingThreads));

        LoadingThreadOptions = settingsService.GetComboOptions<int>("VcdViewer_LoadingThreads");

        WaveFormViewer.SignalRemoved += (_, _) => { MarkIfDirty(); };
    }

    public ObservableCollection<VcdScopeModel> Scopes { get; } = new();

    public VcdScopeModel? SelectedScope
    {
        get => _selectedScope;
        set => SetProperty(ref _selectedScope, value);
    }

    public IVcdSignal? SelectedSignal
    {
        get => _selectedSignal;
        set => SetProperty(ref _selectedSignal, value);
    }

    public int[] LoadingThreadOptions { get; }

    public int LoadingThreads
    {
        get => _loadingThreads;
        set => SetProperty(ref _loadingThreads, value);
    }

    public WaveFormViewModel WaveFormViewer { get; } = new();

    public override string CloseWarningMessage =>
        $"Do you want to save the current view to the file {CurrentFile?.Name}? All added signals will be opened automatically next time!";

    public void PrepareLiveStream()
    {
        Reset();
        _waitForLiveStream = true;
        _isLiveExecution = false;
    }

    private static string GetSaveFilePath(string vcdPath)
    {
        return Path.Combine(Path.GetDirectoryName(vcdPath) ?? "",
            Path.GetFileNameWithoutExtension(vcdPath) + ".vcdconf");
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
        if (_cancellationTokenSource != null) await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource = new CancellationTokenSource();
        Scopes.Clear();

        IDisposable? timer = null;

        try
        {
            var context = !WaveFormViewer.Signals.Any()
                ? await VcdContextManager.LoadContextAsync(GetSaveFilePath(FullPath))
                : null;

            if (context != null) _lastContext = context;

            if (_cancellationTokenSource.IsCancellationRequested) return false;

            var parseLock = LoadingThreads > 1 ? new object() : null;

            _vcdFile = VcdParser.ParseVcdDefinition(FullPath, parseLock);

            Scopes.AddRange(_vcdFile.Definition.Scopes.Where(x => x.Signals.Count != 0 || x.Scopes.Count != 0)
                .Select(x => new VcdScopeModel(x)));

            WaveFormViewer.TimeScale = _vcdFile.Definition.TimeScale;

            var lastTime = !_isLiveExecution ? await VcdParser.TryFindLastTime(FullPath) : 0;

            if (_cancellationTokenSource.IsCancellationRequested) return false;

            if (context?.OpenSignals is null)
            {
                var currentSignals = WaveFormViewer.Signals.ToArray();
                WaveFormViewer.Signals.Clear();
                foreach (var signal in currentSignals)
                    if (_vcdFile.Definition.SignalRegister.TryGetValue(signal.Signal.Id, out var vcdSignal))
                    {
                        var model = WaveFormViewer.AddSignal(vcdSignal);
                        model.DataType = signal.DataType;
                        model.AutomaticFixedPointShift = signal.AutomaticFixedPointShift;
                        model.FixedPointShift = signal.FixedPointShift;
                        WatchWave(model);
                    }
            }
            else
            {
                foreach (var signal in context.OpenSignals)
                    if (_vcdFile.Definition.SignalRegister.TryGetValue(signal.Id, out var vcdSignal))
                    {
                        var model = WaveFormViewer.AddSignal(vcdSignal);
                        model.DataType = signal.DataType;
                        model.AutomaticFixedPointShift = signal.AutomaticFixedPointShift;
                        model.FixedPointShift = signal.FixedPointShift;
                        WatchWave(model);
                    }
            }

            if (lastTime.HasValue) WaveFormViewer.Max = lastTime.Value;

            //Check if a process is writing to this VCD to disable multicore parsing
            var useThreads = _isLiveExecution ? 1 : _loadingThreads;

            //Progress Handling
            var progressParts = new int[LoadingThreads];
            var progress = new Progress<(int part, int progress)>(x =>
            {
                if (x.part < progressParts.Length) progressParts[x.part] = x.progress;
            });

            timer = DispatcherTimer.Run(() =>
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) return false;

                var progressAverage = (int)progressParts.Average();
                ReportProgress(progressAverage, _isLiveExecution);
                return true;
            }, TimeSpan.FromMilliseconds(100), DispatcherPriority.MaxValue);

            await VcdParser.ReadSignalsAsync(FullPath, _vcdFile, progress, _cancellationTokenSource.Token, useThreads,
                parseLock);

            if (_vcdFile != null)
                foreach (var (_, s) in _vcdFile.Definition.SignalRegister)
                    s.Invalidate();

            WaveFormViewer.LoadingMarkerOffset = long.MaxValue;
            Title = CurrentFile is ExternalFile ? $"[{CurrentFile.Name}]" : CurrentFile!.Name;

            LoadingFailed = false;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            LoadingFailed = true;
        }

        timer?.Dispose();

        IsLoading = false;
        CheckIsDirty();

        await Task.Delay(50);
        _isLiveExecution = false;

        return true;
    }

    private void ReportProgress(int progress, bool isLive)
    {
        if (_vcdFile == null) return;

        Title = isLive ? $"{Path.GetFileName(FullPath)} - LIVE" : $"{Path.GetFileName(FullPath)} {progress}%";

        if (_vcdFile.Definition.ChangeTimes.Count != 0)
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
        var model = WaveFormViewer.AddSignal(signal);
        MarkIfDirty();
        WatchWave(model);
    }

    private void WatchWave(WaveModel waveModel)
    {
        waveModel.WhenValueChanged(x => x.DataType).Subscribe(x => { MarkIfDirty(); })
            .DisposeWith(_compositeDisposable);
        waveModel.WhenValueChanged(x => x.FixedPointShift).Subscribe(x => { MarkIfDirty(); })
            .DisposeWith(_compositeDisposable);
        waveModel.WhenValueChanged(x => x.AutomaticFixedPointShift).Subscribe(x => { MarkIfDirty(); })
            .DisposeWith(_compositeDisposable);
    }

    protected override void Reset()
    {
        _cancellationTokenSource?.Cancel();
        _compositeDisposable.Dispose();
        _compositeDisposable = new CompositeDisposable();

        //Manual cleanup because ViewModel is stuck somewhere
        WaveFormViewer.Signals.Clear();
        SelectedSignal = null;
        SelectedScope = null;
        Scopes.Clear();

        _vcdFile = null;
    }

    public override async Task<bool> SaveAsync()
    {
        if (!_settingsService.GetSettingValue<bool>("VcdViewer_SaveView_Enable")) return true;
        var context = new VcdContext(WaveFormViewer.Signals.Select(x =>
            new VcdContextSignal(x.Signal.Id, x.DataType, x.AutomaticFixedPointShift, x.FixedPointShift)));
        var result = await VcdContextManager.SaveContextAsync(GetSaveFilePath(FullPath), context);
        if (result)
        {
            IsDirty = false;
            _lastContext = context;
        }

        return result;
    }

    private void MarkIfDirty()
    {
        if (!_settingsService.GetSettingValue<bool>("VcdViewer_SaveView_Enable")) return;
        IsDirty = CheckIsDirty();
    }

    private bool CheckIsDirty()
    {
        if (_lastContext?.OpenSignals == null) return true;
        var openSignalsContext = _lastContext.OpenSignals.ToArray();

        if (openSignalsContext.Length != WaveFormViewer.Signals.Count) return true;

        for (var i = 0; i < openSignalsContext.Length; i++)
            if (openSignalsContext[i].Id != WaveFormViewer.Signals[i].Signal.Id ||
                openSignalsContext[i].DataType != WaveFormViewer.Signals[i].DataType
                || openSignalsContext[i].AutomaticFixedPointShift != WaveFormViewer.Signals[i].AutomaticFixedPointShift
                || openSignalsContext[i].FixedPointShift != WaveFormViewer.Signals[i].FixedPointShift)
                return true;
        return false;
    }
}