using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.Models;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

/// <summary>
///     Unteres Panel: Steuerleiste der Debug-Session sowie die Reiter Console, Registers
///     und Debugger Console.
/// </summary>
public partial class DebuggerViewModel : ExtendedTool
{
    public const string IconKey = "Material.BugReport";

    private readonly ILogger _logger;
    private readonly IMainDockService _mainDockService;
    private readonly IPaths _paths;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ISettingsService _settingsService;

    /// <summary>
    ///     Eingabezeile der Debugger Console, ueber die sich Kommandos direkt an das Backend
    ///     schicken lassen.
    /// </summary>
    [ObservableProperty] private string _commandText = string.Empty;

    [ObservableProperty] private bool _isRunning;

    [ObservableProperty] private bool _isSessionActive;

    private GdbSession? _session;

    /// <summary>
    ///     Reihenfolge der Reiter: 0 Console, 1 Registers, 2 Debugger Console.
    /// </summary>
    private const int DebuggerConsoleTabIndex = 2;

    [ObservableProperty] private int _selectedTabIndex;

    [ObservableProperty] private string _statusText = "No session";

    public DebuggerViewModel(ILogger logger, ISettingsService settingsService, IPaths paths,
        IProjectExplorerService projectExplorerService, IMainDockService mainDockService) : base(IconKey)
    {
        _logger = logger;
        _settingsService = settingsService;
        _paths = paths;
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;

        Id = "Debug";
        Title = "Debug";
    }

    /// <summary>Ausgaben des debuggten Programms.</summary>
    public ObservableCollection<ConsoleLine> Console { get; } = [];

    /// <summary>Registerinhalte der laufenden Session.</summary>
    public ObservableCollection<RegisterRow> Registers { get; } = [];

    /// <summary>Roher Verkehr mit dem Debugger-Backend, inklusive abgesetzter Kommandos.</summary>
    public ObservableCollection<ConsoleLine> DebuggerConsole { get; } = [];

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        var gdbPath = ResolveGdbPath();
        if (gdbPath == null)
        {
            StatusText = "No GDB configured";
            AppendToDebuggerConsole("No GDB binary found. Set the path under Tools > Debugger.");
            return;
        }

        // Ohne ausgewaehltes Zielprogramm startet GDB ohne Target. Das reicht, um die
        // Verbindung zu pruefen; der Pfad zur ELF-Datei kommt spaeter aus dem Projekt.
        var target = _projectExplorerService.ActiveProject?.FullPath ?? _paths.ProjectsDirectory;
        if (!target.EndsWith(Path.DirectorySeparatorChar)) target += Path.DirectorySeparatorChar;

        DebuggerConsole.Clear();
        StatusText = "Starting GDB...";

        // Beim Start interessiert der Verkehr mit dem Backend - also den passenden Reiter
        // in den Vordergrund holen und das Panel selbst sichtbar machen.
        SelectedTabIndex = DebuggerConsoleTabIndex;
        _mainDockService.Show(this, DockShowLocation.Bottom);

        _session = new GdbSession(gdbPath, target, false, _logger, _projectExplorerService, _mainDockService);
        _session.OutputReceived += OnOutputReceived;
        _session.CommandRun += OnCommandRun;
        _session.Exited += OnExited;

        IsSessionActive = true;

        var started = await _session.RunAsync();

        if (!started)
        {
            AppendToDebuggerConsole("GDB could not be started.");
            StatusText = "Start failed";
            DetachSession();
            IsSessionActive = false;
            return;
        }

        StatusText = $"GDB running ({Path.GetFileName(gdbPath)})";
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void Stop()
    {
        StatusText = "Stopping...";

        try
        {
            _session?.Stop();
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        DetachSession();
        IsSessionActive = false;
        IsRunning = false;
        StatusText = "No session";
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void Pause()
    {
        _session?.Pause();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void Continue()
    {
        _session?.Continue();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void StepOver()
    {
        _session?.Next();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void StepInto()
    {
        _session?.Step();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void StepOut()
    {
        _session?.Finish();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void SendCommand()
    {
        if (string.IsNullOrWhiteSpace(CommandText) || _session == null) return;

        var command = CommandText;
        CommandText = string.Empty;
        _ = Task.Run(() => _session.RunCommandAsync(command));
    }

    /// <summary>
    ///     Ein explizit gesetztes Setting hat Vorrang; ist es leer, wird bei jedem Start neu gesucht.
    /// </summary>
    private string? ResolveGdbPath()
    {
        if (!_settingsService.HasSetting(DebuggerModule.GdbPathSetting))
            return GdbLocator.Find(_paths.NativeToolsDirectory);

        var configured = _settingsService.GetSettingValue<string>(DebuggerModule.GdbPathSetting);

        if (string.IsNullOrWhiteSpace(configured))
            return GdbLocator.Find(_paths.NativeToolsDirectory);

        return File.Exists(configured) ? configured : null;
    }

    private void DetachSession()
    {
        if (_session == null) return;

        _session.OutputReceived -= OnOutputReceived;
        _session.CommandRun -= OnCommandRun;
        _session.Exited -= OnExited;
        _session = null;
    }

    private void OnOutputReceived(object? sender, string line)
    {
        // GdbSession reicht die rohe MI-Zeile durch; erst hier wird daraus lesbarer Text.
        if (GdbOutputFormatter.Format(line) is { } formatted) AppendToDebuggerConsole(formatted);
    }

    private void OnCommandRun(object? sender, string command)
    {
        AppendToDebuggerConsole($"> {command.Trim()}", true);
    }

    private void OnExited(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AppendToDebuggerConsole("GDB exited.");
            DetachSession();
            IsSessionActive = false;
            IsRunning = false;
            StatusText = "No session";
        });
    }

    private void AppendToDebuggerConsole(string line, bool isCommand = false)
    {
        Dispatcher.UIThread.Post(() => DebuggerConsole.Add(new ConsoleLine(line, isCommand)));
    }

    private bool CanStart()
    {
        return !IsSessionActive;
    }

    partial void OnIsSessionActiveChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ContinueCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        StepOverCommand.NotifyCanExecuteChanged();
        StepIntoCommand.NotifyCanExecuteChanged();
        StepOutCommand.NotifyCanExecuteChanged();
        SendCommandCommand.NotifyCanExecuteChanged();
    }
}
