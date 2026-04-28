using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

public class DebuggerViewModel : ExtendedTool
{
    public const string IconKey = "VsCodeLight.Debug";

    private readonly IDebuggerService _debuggerService;
    private string? _commandText;
    private string? _executablePath;
    private bool _isDebugging;
    private bool _isRunning;
    private DebuggerAdapterItemViewModel? _selectedAdapter;
    private string? _statusText;

    public DebuggerViewModel(IDebuggerService debuggerService) : base(IconKey)
    {
        _debuggerService = debuggerService;
        Id = "Debug";

        AvailableAdapters = new ObservableCollection<DebuggerAdapterItemViewModel>(
            _debuggerService.Adapters.Select(x => new DebuggerAdapterItemViewModel(x.Id, x.DisplayName, x.Description)));
        SelectedAdapter = AvailableAdapters.FirstOrDefault();

        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsDebugging && SelectedAdapter != null);
        StopCommand = new RelayCommand(_debuggerService.Stop, () => IsDebugging);
        ContinueCommand = new RelayCommand(_debuggerService.Continue, () => IsDebugging && !IsRunning);
        PauseCommand = new RelayCommand(_debuggerService.Pause, () => IsDebugging && IsRunning);
        StepIntoCommand = new RelayCommand(_debuggerService.StepInto, () => IsDebugging && !IsRunning);
        StepOverCommand = new RelayCommand(_debuggerService.StepOver, () => IsDebugging && !IsRunning);
        StepOutCommand = new RelayCommand(_debuggerService.StepOut, () => IsDebugging && !IsRunning);
        BrowseExecutableCommand = new AsyncRelayCommand(BrowseExecutableAsync);
        ExecuteRawCommand = new AsyncRelayCommand(ExecuteRawCommandAsync, CanExecuteRawCommand);

        _debuggerService.StateChanged += (_, _) => Dispatcher.UIThread.Post(RefreshState);
        AvailableAdapters.CollectionChanged += OnAvailableAdaptersChanged;
        RefreshState();
    }

    public ObservableCollection<DebuggerAdapterItemViewModel> AvailableAdapters { get; }

    public AsyncRelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ContinueCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand StepIntoCommand { get; }
    public RelayCommand StepOverCommand { get; }
    public RelayCommand StepOutCommand { get; }
    public AsyncRelayCommand BrowseExecutableCommand { get; }
    public AsyncRelayCommand ExecuteRawCommand { get; }

    public DebuggerAdapterItemViewModel? SelectedAdapter
    {
        get => _selectedAdapter;
        set
        {
            if (SetProperty(ref _selectedAdapter, value))
            {
                StartCommand?.NotifyCanExecuteChanged();
                ExecuteRawCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ExecutablePath
    {
        get => _executablePath;
        set => SetProperty(ref _executablePath, value);
    }

    public string? CommandText
    {
        get => _commandText;
        set
        {
            if (SetProperty(ref _commandText, value))
                ExecuteRawCommand?.NotifyCanExecuteChanged();
        }
    }

    public string? StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsDebugging
    {
        get => _isDebugging;
        private set
        {
            if (SetProperty(ref _isDebugging, value))
                NotifyCommands();
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
                NotifyCommands();
        }
    }

    public bool SupportsRawCommand => _debuggerService.CurrentSession?.SupportsRawCommand ?? false;

    public override void InitializeContent()
    {
        base.InitializeContent();
        Title = "Debugger";
    }

    private async Task StartAsync()
    {
        if (SelectedAdapter == null)
            return;

        if (string.IsNullOrWhiteSpace(ExecutablePath))
        {
            await BrowseExecutableAsync();
            if (string.IsNullOrWhiteSpace(ExecutablePath))
                return;
        }

        await _debuggerService.StartAsync(new DebugLaunchRequest(SelectedAdapter.Id, ExecutablePath!));
    }

    private async Task BrowseExecutableAsync()
    {
        var topLevel = GetMainWindowTopLevel();
        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select executable to debug",
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file != null)
            ExecutablePath = file.TryGetLocalPath();
    }

    private async Task ExecuteRawCommandAsync()
    {
        var command = CommandText?.Trim();
        if (string.IsNullOrWhiteSpace(command))
            return;

        await _debuggerService.ExecuteRawCommandAsync(command);
        CommandText = string.Empty;
    }

    private bool CanExecuteRawCommand()
    {
        return IsDebugging && SupportsRawCommand && !string.IsNullOrWhiteSpace(CommandText);
    }

    private static TopLevel? GetMainWindowTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    private void RefreshState()
    {
        IsDebugging = _debuggerService.IsActive;
        IsRunning = _debuggerService.CurrentState.IsRunning;

        var frame = _debuggerService.CurrentState.CurrentFrame;
        StatusText = !IsDebugging
            ? "Idle"
            : IsRunning
                ? "Running"
                : frame is { FullPath: { Length: > 0 }, Line: > 0 }
                    ? $"Stopped at {Path.GetFileName(frame.FullPath)}:{frame.Line}"
                    : "Stopped";

        NotifyCommands();
    }

    private void NotifyCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ContinueCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        StepIntoCommand.NotifyCanExecuteChanged();
        StepOverCommand.NotifyCanExecuteChanged();
        StepOutCommand.NotifyCanExecuteChanged();
        ExecuteRawCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SupportsRawCommand));
    }

    private void OnAvailableAdaptersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (SelectedAdapter == null)
            SelectedAdapter = AvailableAdapters.FirstOrDefault();
    }
}
