using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OneWare.Debugger.Models;
using OneWare.Essentials.Debugger;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

/// <summary>
///     Unteres Panel: Steuerleiste der Debug-Session sowie die Reiter Registers und
///     Debugger Console.
/// </summary>
/// <remarks>
///     Bindet ausschliesslich an <see cref="IDebuggerService" />, nie an eine Session. Damit muss
///     beim Starten und Beenden nichts umgehaengt werden, und das Panel funktioniert auch dann,
///     wenn es erst waehrend einer laufenden Session geoeffnet wird.
/// </remarks>
public partial class DebuggerViewModel : ExtendedTool
{
    public const string IconKey = "Material.BugReport";

    private readonly IDebuggerService _debuggerService;
    private readonly ILogger _logger;
    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ISettingsService _settingsService;

    /// <summary>
    ///     Reihenfolge der Reiter: 0 Registers, 1 Memory, 2 Debugger Console.
    /// </summary>
    private const int DebuggerConsoleTabIndex = 2;

    /// <summary>
    ///     Adresse, die der Eingabezeile im Memory-Reiter entnommen und beim Hinzufuegen zur
    ///     Beobachtungsliste gemacht wird.
    /// </summary>
    [ObservableProperty] private string _memoryAddressText = string.Empty;

    [ObservableProperty] private MemoryRow? _selectedMemoryRow;

    /// <summary>
    ///     Die Programmdatei, mit der gestartet wird. <c>null</c> heisst: ohne Symbole, so wie es
    ///     ein Ziel verlangt, das sein Programm selbst haelt.
    /// </summary>
    [ObservableProperty] private ExecutableOption? _selectedExecutable;

    /// <summary>
    ///     Eingabezeile der Debugger Console, ueber die sich Kommandos direkt an das Backend
    ///     schicken lassen.
    /// </summary>
    [ObservableProperty] private string _commandText = string.Empty;

    [ObservableProperty] private bool _isRunning;

    [ObservableProperty] private bool _isSessionActive;

    [ObservableProperty] private int _selectedTabIndex;

    [ObservableProperty] private string _statusText = "No session";

    private IDebugSession? _attachedSession;

    public DebuggerViewModel(IDebuggerService debuggerService, ILogger logger, ISettingsService settingsService,
        IProjectExplorerService projectExplorerService, IMainDockService mainDockService) : base(IconKey)
    {
        _debuggerService = debuggerService;
        _logger = logger;
        _settingsService = settingsService;
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;

        Id = "Debug";
        Title = "Debug";

        _debuggerService.StateChanged += OnDebuggerStateChanged;

        RefreshExecutables();
    }

    /// <summary>
    ///     Zur Auswahl stehende Programmdateien: die ELF-Dateien des aktiven Projekts und alles,
    ///     was ueber den Durchsuchen-Knopf dazugekommen ist.
    /// </summary>
    public ObservableCollection<ExecutableOption> Executables { get; } = [];

    /// <summary>Registerinhalte, wie sie beim letzten Halt gelesen wurden.</summary>
    public ObservableCollection<RegisterRow> Registers { get; } = [];

    /// <summary>
    ///     Vom Benutzer beobachtete Speicheradressen. Bleiben ueber Sessions hinweg stehen, damit
    ///     man dieselben Adressen nach einem Neustart nicht wieder eintippen muss.
    /// </summary>
    public ObservableCollection<MemoryRow> MemoryWatches { get; } = [];

    /// <summary>Verkehr mit dem Debugger-Backend, inklusive abgesetzter Kommandos.</summary>
    public ObservableCollection<ConsoleLine> DebuggerConsole { get; } = [];

    /// <summary>
    ///     Laesst den Benutzer eine Programmdatei ausserhalb des Projekts waehlen und uebernimmt
    ///     sie in die Auswahl.
    /// </summary>
    [RelayCommand]
    private async Task BrowseExecutableAsync()
    {
        var owner = _mainDockService.GetWindowOwner(this);
        if (owner == null) return;

        var path = await StorageProviderHelper.SelectFileAsync(owner, "Select Executable",
            _projectExplorerService.ActiveProject?.FullPath,
            new FilePickerFileType("Executables") { Patterns = ["*.elf", "*.exe", "*.out", "*"] });

        if (path == null) return;

        var option = new ExecutableOption(path, Path.GetFileName(path));

        if (Executables.FirstOrDefault(x => x.Path == path) is { } existing)
        {
            SelectedExecutable = existing;
            return;
        }

        Executables.Add(option);
        SelectedExecutable = option;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        // Vor jedem Start neu einlesen: nach einem Neubau des Projekts liegt die ELF-Datei sonst
        // da, taucht aber erst nach einem Neustart des Studios in der Auswahl auf.
        RefreshExecutables();

        DebuggerConsole.Clear();
        StatusText = "Starting GDB...";

        // Beim Start interessiert der Verkehr mit dem Backend - also den passenden Reiter in den
        // Vordergrund holen und das Panel selbst sichtbar machen.
        SelectedTabIndex = DebuggerConsoleTabIndex;
        _mainDockService.Show(this, DockShowLocation.Bottom);

        var request = BuildLaunchRequest();

        if (!await _debuggerService.StartAsync(request))
        {
            AppendLine("GDB could not be started.");
            StatusText = "Start failed";
            return;
        }

        // Erst jetzt, nicht vor dem Start: sonst stuende der Hinweis noch vor der Versionszeile von
        // GDB und laese sich wie ein Abbruch, obwohl gerade nichts fehlgeschlagen ist.
        if (request.ExecutablePath == null && request.RemoteEndpoint == null)
            AppendLine("No executable in the active project and no remote endpoint under " +
                       "Tools > Debugger - running without a target. Registers, memory and stepping " +
                       "need one.");
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private Task StopAsync()
    {
        StatusText = "Stopping...";
        return _debuggerService.StopAsync();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private Task PauseAsync()
    {
        return _debuggerService.PauseAsync();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private Task ContinueAsync()
    {
        return _debuggerService.ContinueAsync();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private Task StepOverAsync()
    {
        return _debuggerService.StepOverAsync();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private Task StepIntoAsync()
    {
        return _debuggerService.StepIntoAsync();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private Task StepOutAsync()
    {
        return _debuggerService.StepOutAsync();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(CommandText)) return Task.CompletedTask;

        var command = CommandText;
        CommandText = string.Empty;
        return _debuggerService.SendRawCommandAsync(command);
    }

    /// <summary>
    ///     Nimmt die eingetippte Adresse in die Beobachtungsliste auf und liest sie sofort, sofern
    ///     das Ziel gerade haelt. Ohne das sofortige Lesen stuende die neue Zeile bis zum naechsten
    ///     Halt leer da, und man wuesste nicht, ob die Adresse ueberhaupt lesbar ist.
    /// </summary>
    [RelayCommand]
    private async Task AddMemoryWatchAsync()
    {
        if (string.IsNullOrWhiteSpace(MemoryAddressText)) return;

        var row = new MemoryRow { Address = MemoryAddressText.Trim() };
        MemoryWatches.Add(row);
        MemoryAddressText = string.Empty;

        row.PropertyChanged += OnMemoryRowChanged;

        await RefreshMemoryRowAsync(row);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveMemoryWatch))]
    private void RemoveMemoryWatch()
    {
        if (SelectedMemoryRow is not { } row) return;

        row.PropertyChanged -= OnMemoryRowChanged;
        MemoryWatches.Remove(row);
        SelectedMemoryRow = null;
    }

    private bool CanRemoveMemoryWatch()
    {
        return SelectedMemoryRow is not null;
    }

    partial void OnSelectedMemoryRowChanged(MemoryRow? value)
    {
        RemoveMemoryWatchCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Eine im Raster bearbeitete Adresse oder Laenge wird sofort neu gelesen. Der Wert selbst
    ///     ist ausgenommen, sonst loeste das Schreiben des Ergebnisses das naechste Lesen aus.
    /// </summary>
    private void OnMemoryRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MemoryRow row) return;
        if (e.PropertyName is not (nameof(MemoryRow.Address) or nameof(MemoryRow.Length))) return;

        _ = RefreshMemoryRowAsync(row);
    }

    private async Task RefreshMemoryRowAsync(MemoryRow row)
    {
        if (!_debuggerService.IsActive)
        {
            row.Value = string.Empty;
            return;
        }

        if (_debuggerService.State.IsRunning)
        {
            row.Value = "target running";
            return;
        }

        row.Value = await _debuggerService.ReadMemoryAsync(row.Address, row.Length) ?? "unreadable";
    }

    private async Task RefreshMemoryAsync()
    {
        // Nacheinander: das Backend beantwortet ohnehin nur ein Kommando zur Zeit, und in der
        // Reihenfolge der Liste zu lesen haelt die Anzeige nachvollziehbar.
        foreach (var row in MemoryWatches.ToArray()) await RefreshMemoryRowAsync(row);
    }

    /// <summary>
    ///     Stellt zusammen, was gedebuggt werden soll: die Programmdatei aus dem aktiven Projekt und,
    ///     sofern eingerichtet, die Adresse des Stubs.
    /// </summary>
    /// <remarks>
    ///     Fehlt beides, wird trotzdem gestartet. GDB laeuft dann ohne Ziel, und genau das ist der
    ///     Fall, in dem man es braucht: die Debugger Console beantwortet Kommandos, man kann die
    ///     Installation pruefen und sich von Hand an ein Ziel haengen. Den Start hier abzulehnen
    ///     hiesse, dem Nutzer den Weg zu verbauen, auf dem er das Problem ueberhaupt findet.
    /// </remarks>
    private DebugLaunchRequest BuildLaunchRequest()
    {
        var endpoint = _settingsService.GetSettingValue<string>(DebuggerModule.RemoteEndpointSetting);
        if (string.IsNullOrWhiteSpace(endpoint)) endpoint = null;

        return new DebugLaunchRequest(GdbDebugAdapter.AdapterId, SelectedExecutable?.Path, endpoint,
            _projectExplorerService.ActiveProject?.FullPath);
    }

    /// <summary>
    ///     Liest die Auswahlliste neu ein: alle ELF-Dateien des aktiven Projekts, dazu alles, was
    ///     der Benutzer von Hand dazugelegt hat.
    /// </summary>
    /// <remarks>
    ///     Die getroffene Auswahl bleibt stehen, solange ihr Pfad noch existiert. Sie bei jedem
    ///     Einlesen zurueckzusetzen hiesse, dass ein Neubau des Projekts die Einstellung des
    ///     Benutzers verwirft.
    /// </remarks>
    private void RefreshExecutables()
    {
        var previous = SelectedExecutable;
        var browsed = Executables.Where(x => !IsUnderActiveProject(x.Path)).ToList();

        Executables.Clear();

        foreach (var option in FindProjectExecutables().Concat(browsed).DistinctBy(x => x.Path))
            Executables.Add(option);

        SelectedExecutable = Executables.FirstOrDefault(x => x.Path == previous?.Path)
                             ?? Executables.FirstOrDefault();
    }

    private bool IsUnderActiveProject(string path)
    {
        if (_projectExplorerService.ActiveProject is not { } project) return false;

        return path.StartsWith(project.FullPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Sucht die Programmdateien im aktiven Projekt. Ein Ziel wie der SVNR haelt sein Programm
    ///     selbst und bringt gar keine mit - dann bleibt die Liste leer, und GDB haengt sich ohne
    ///     Symbole an.
    /// </summary>
    private IEnumerable<ExecutableOption> FindProjectExecutables()
    {
        if (_projectExplorerService.ActiveProject is not { } project) return [];

        try
        {
            return Directory.EnumerateFiles(project.FullPath, "*.elf", SearchOption.AllDirectories)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(path => new ExecutableOption(path,
                    Path.GetRelativePath(project.FullPath, path)))
                .ToList();
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return [];
        }
    }

    private void OnDebuggerStateChanged(object? sender, EventArgs e)
    {
        var wasActive = IsSessionActive;

        IsSessionActive = _debuggerService.IsActive;
        IsRunning = _debuggerService.State.IsRunning;

        if (!IsSessionActive)
        {
            if (wasActive) AppendLine("GDB exited.");
            DetachFromSession();
            Registers.Clear();
            foreach (var row in MemoryWatches) row.Value = string.Empty;
            StatusText = "No session";
            return;
        }

        if (!wasActive) AttachToSession();

        UpdateRegisters(_debuggerService.State.Registers);
        StatusText = DescribeState(_debuggerService.State);

        _ = RefreshMemoryAsync();
        _ = JumpToCurrentFrameAsync(_debuggerService.State);
    }

    private void AttachToSession()
    {
        if (_debuggerService.CurrentSession is not { } session) return;

        _attachedSession = session;
        session.OutputReceived += OnOutputReceived;
        session.CommandSent += OnCommandSent;
    }

    private void DetachFromSession()
    {
        if (_attachedSession == null) return;

        _attachedSession.OutputReceived -= OnOutputReceived;
        _attachedSession.CommandSent -= OnCommandSent;
        _attachedSession = null;
    }

    private static string DescribeState(DebugSessionState state)
    {
        if (state.IsRunning) return "Running";
        if (state.CurrentFrame is not { } frame) return "Connected";

        if (frame.File is { } file && frame.Line > 0)
            return $"Stopped at {Path.GetFileName(file)}:{frame.Line}";

        // Ohne Symbole ist der Programmzaehler das Einzige, was den Halt beschreibt.
        return frame.Address is { } address ? $"Stopped at {address}" : "Stopped";
    }

    /// <summary>
    ///     Aktualisiert die Registeranzeige an Ort und Stelle. Die Sammlung neu aufzubauen wuerde
    ///     bei jedem Einzelschritt die Bildlaufposition zuruecksetzen.
    /// </summary>
    private void UpdateRegisters(IReadOnlyList<RegisterValue> registers)
    {
        if (registers.Count == 0) return;

        for (var i = 0; i < registers.Count; i++)
        {
            if (i < Registers.Count && Registers[i].Name == registers[i].Name)
            {
                Registers[i].Value = registers[i].Value;
                continue;
            }

            var row = new RegisterRow { Name = registers[i].Name, Value = registers[i].Value };

            if (i < Registers.Count) Registers[i] = row;
            else Registers.Add(row);
        }

        while (Registers.Count > registers.Count) Registers.RemoveAt(Registers.Count - 1);
    }

    private async Task JumpToCurrentFrameAsync(DebugSessionState state)
    {
        if (state.CurrentFrame is not { File: { } file, Line: > 0 } frame) return;
        if (!File.Exists(file)) return;

        try
        {
            if (await _mainDockService.OpenFileAsync(file) is IEditor editor) editor.JumpToLine(frame.Line);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    private void OnOutputReceived(object? sender, string line)
    {
        AppendLine(line);
    }

    private void OnCommandSent(object? sender, string command)
    {
        AppendLine($"> {command}", true);
    }

    /// <summary>
    ///     Die Session meldet sich aus ihrem Lesethread; die Sammlung haengt aber an der Ansicht
    ///     und darf nur vom UI-Thread angefasst werden.
    /// </summary>
    private void AppendLine(string line, bool isCommand = false)
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
