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

    private string? _executablePath;
    private bool _isDebugging;
    private bool _isRunning;

    public DebuggerViewModel(IDebuggerService debuggerService) : base(IconKey)
    {
        _debuggerService = debuggerService;
        Id = "Debug";

        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsDebugging);
        StopCommand = new RelayCommand(_debuggerService.Stop, () => IsDebugging);
        ContinueCommand = new RelayCommand(_debuggerService.Continue, () => IsDebugging && !IsRunning);
        PauseCommand = new RelayCommand(_debuggerService.Pause, () => IsDebugging && IsRunning);
        StepCommand = new RelayCommand(_debuggerService.Step, () => IsDebugging && !IsRunning);
        NextCommand = new RelayCommand(_debuggerService.Next, () => IsDebugging && !IsRunning);
        FinishCommand = new RelayCommand(_debuggerService.Finish, () => IsDebugging && !IsRunning);
        BrowseExecutableCommand = new AsyncRelayCommand(BrowseExecutableAsync);

        _debuggerService.StateChanged += (_, _) => Dispatcher.UIThread.Post(RefreshState);
        RefreshState();
    }

    public AsyncRelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ContinueCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand StepCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand FinishCommand { get; }
    public AsyncRelayCommand BrowseExecutableCommand { get; }

    public string? ExecutablePath
    {
        get => _executablePath;
        set => SetProperty(ref _executablePath, value);
    }

    public bool IsDebugging
    {
        get => _isDebugging;
        private set
        {
            if (SetProperty(ref _isDebugging, value)) NotifyCommands();
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value)) NotifyCommands();
        }
    }

    public override void InitializeContent()
    {
        base.InitializeContent();
        Title = "Debug";
    }

    private async Task StartAsync()
    {
        if (string.IsNullOrWhiteSpace(ExecutablePath))
        {
            await BrowseExecutableAsync();
            if (string.IsNullOrWhiteSpace(ExecutablePath)) return;
        }

        await _debuggerService.StartAsync(ExecutablePath!);
    }

    private async Task BrowseExecutableAsync()
    {
        var topLevel = GetMainWindowTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select executable to debug",
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file != null) ExecutablePath = file.TryGetLocalPath();
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
        IsRunning = _debuggerService.CurrentSession?.IsRunning ?? false;
    }

    private void NotifyCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ContinueCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        StepCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }
}