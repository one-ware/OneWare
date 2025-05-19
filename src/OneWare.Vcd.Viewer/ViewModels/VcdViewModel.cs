using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Vcd.Parser;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Viewer.Context;
using OneWare.Vcd.Viewer.Models;
using OneWare.WaveFormViewer.ViewModels;
using Serilog;

namespace OneWare.Vcd.Viewer.ViewModels;

public class VcdViewModel : ExtendedDocument, IStreamableDocument
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;

    private CancellationTokenSource _cancellationTokenSource = new();
    private CompositeDisposable _compositeDisposable = new();
    private bool _isLiveExecution;
    private bool _waitLiveExecution;

    private VcdContext? _lastContext;
    private int _loadingThreads;
    private VcdScopeModel? _selectedScope;
    private IVcdSignal? _selectedSignal;
    private VcdFile? _vcdFile;

    public VcdViewModel(string fullPath, IProjectExplorerService projectExplorerService, IDockService dockService,
        ISettingsService settingsService, IWindowService windowService, ILogger logger)
        : base(fullPath, projectExplorerService, dockService, windowService)
    {
        _settingsService = settingsService;
        _logger = logger;

        WaveFormViewer.ExtendSignals = true;

        Title = $"Loading {Path.GetFileName(fullPath)}";

        settingsService.GetSettingObservable<int>("VcdViewer_LoadingThreads").Subscribe(x => { LoadingThreads = x; });
        _ = settingsService.Bind("VcdViewer_LoadingThreads", this.WhenValueChanged(x => x.LoadingThreads));

        LoadingThreadOptions = settingsService.GetComboOptions<int>("VcdViewer_LoadingThreads");

        WaveFormViewer.Signals.ToObservableChangeSet().Subscribe(_ => MarkIfDirty());
        WaveFormViewer.Signals.ToObservableChangeSet().Transform(signal =>
                Observable.Merge(signal.WhenValueChanged(x => x.FixedPointShift).Select(object? (_) => null),
                    signal.WhenValueChanged(x => x.AutomaticFixedPointShift).Select(object? (_) => null),
                    signal.WhenValueChanged(x => x.DataType).Select(object? (_) => null)))
            .MergeMany(x => x)
            .Subscribe(_ => MarkIfDirty());
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
        _waitLiveExecution = true;
    }

    private static string GetSaveFilePath(string vcdPath)
    {
        return Path.Combine(Path.GetDirectoryName(vcdPath) ?? "",
            Path.GetFileNameWithoutExtension(vcdPath) + ".vcdconf");
    }

    public override void InitializeContent()
    {
        base.InitializeContent();
        if (_isLiveExecution) Title = $"{Path.GetFileName(FullPath)} - LIVE";
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
            if (_waitLiveExecution)
            {
                _isLiveExecution = true;
                _waitLiveExecution = false;
            }
            _ = LoadAsync();
        }
    }

    private async Task<bool> LoadAsync()
    {
        Reset();

        IsLoading = true;
        _cancellationTokenSource = new CancellationTokenSource();

        Scopes.Clear();

        try
        {
            var live = _isLiveExecution;
            var token = _cancellationTokenSource.Token;
            LoadingFailed = !await LoadInternalAsync(token);

            if (live && token.IsCancellationRequested)
            {
                LoadingFailed = !await LoadInternalAsync(token);
            }

            WaveFormViewer.LoadingMarkerOffset = long.MaxValue;
            Title = CurrentFile is ExternalFile ? $"[{CurrentFile.Name}]" : CurrentFile!.Name;

            if (live)
            {
                await Task.Delay(200);
                _isLiveExecution = false;
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error while loading VCD");
            LoadingFailed = true;
        }

        IsLoading = false;
        CheckIsDirty();

        await Task.Delay(200);

        return true;
    }

    private async Task<bool> LoadInternalAsync(CancellationToken cancellationToken)
    {
        var context = !WaveFormViewer.Signals.Any()
            ? await VcdContextManager.LoadContextAsync(GetSaveFilePath(FullPath))
            : null;

        if (context != null) _lastContext = context;

        if (cancellationToken.IsCancellationRequested) return false;

        var parseLock = LoadingThreads > 1 ? new object() : null;

        _vcdFile = VcdParser.ParseVcdDefinition(FullPath, parseLock);

        Scopes.AddRange(_vcdFile.Definition.Scopes.Where(x => x.Signals.Count != 0 || x.Scopes.Count != 0)
            .Select(x => new VcdScopeModel(x)));

        WaveFormViewer.TimeScale = _vcdFile.Definition.TimeScale;

        var lastTime = !_isLiveExecution ? await VcdParser.TryFindLastTime(FullPath) : 0;

        if (cancellationToken.IsCancellationRequested) return false;

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
                }
        }

        if (lastTime.HasValue) WaveFormViewer.Max = lastTime.Value;

        var useThreads = _isLiveExecution ? 1 : _loadingThreads;

        var progressParts = new int[LoadingThreads];
        var progress = new Progress<(int part, int progress)>(x =>
        {
            if (x.part < progressParts.Length) progressParts[x.part] = x.progress;
        });

        var disposable = DispatcherTimer.Run(() =>
        {
            if (cancellationToken.IsCancellationRequested) return false;

            var progressAverage = (int)progressParts.Average();
            ReportProgress(progressAverage, _isLiveExecution);
            return true;
        }, TimeSpan.FromMilliseconds(100), DispatcherPriority.MaxValue)
            .DisposeWith(_compositeDisposable);

        await VcdParser.ReadSignalsAsync(FullPath, _vcdFile, progress, cancellationToken, useThreads,
            parseLock);

        disposable.Dispose();

        if (_vcdFile != null)
            foreach (var (_, s) in _vcdFile.Definition.SignalRegister)
                s.Invalidate();

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
        WaveFormViewer.AddSignal(signal);
        MarkIfDirty();
    }

    protected override void Reset()
    {
        _cancellationTokenSource.Cancel();
        _compositeDisposable.Dispose();
        _compositeDisposable = new CompositeDisposable();

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
