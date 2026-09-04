using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OneWare.Debugger.ViewModels.Main;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

// Unteres Panel: die Steuerleiste der Debug-Session und darunter der Reiterbereich.
// Hier laeuft alles zusammen - dies ist der einzige Ort, der IDebuggerService abonniert und
// sich an die Sitzung haengt. Die Reiter bekommen ihren Zustand von hier gereicht und muessen
// selbst nichts abonnieren.
// Gebunden wird ausschliesslich an IDebuggerService, nie an eine Session. Damit muss beim
// Starten und Beenden nichts umgehaengt werden, und das Panel funktioniert auch dann, wenn es
// erst waehrend einer laufenden Session geoeffnet wird.
public partial class DebuggerViewModel : ExtendedTool
{
    public const string IconKey = "Material.BugReport";

    private readonly IDebuggerService _debuggerService;
    private readonly ILogger _logger;
    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private bool _isRunning;

    // Ein Vorbereiter arbeitet gerade. Es gibt dann noch keine Sitzung, aber starten darf man
    // trotzdem nicht mehr - IsSessionActive allein wuerde den Knopf offen lassen.
    [ObservableProperty] private bool _isPreparing;

    [ObservableProperty] private bool _isSessionActive;

    [ObservableProperty] private string _statusText = "No session";

    private IDebugSession? _attachedSession;

    public DebuggerViewModel(IDebuggerService debuggerService, ILogger logger, ISettingsService settingsService,
        IProjectExplorerService projectExplorerService, IMainDockService mainDockService,
        MainPanelViewModel mainPanel) : base(IconKey)
    {
        _debuggerService = debuggerService;
        _logger = logger;
        _settingsService = settingsService;
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;

        // Vor dem Abo gesetzt -> schon der erste Zustandswechsel greift auf die Reiter zu.
        MainPanel = mainPanel;

        Id = "Debugger";
        Title = "Debugger";

        _debuggerService.StateChanged += OnDebuggerStateChanged;
    }

    // Der Reiterbereich unter der Leiste.
    public MainPanelViewModel MainPanel { get; }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        // Beim Start interessiert der Verkehr mit dem Backend - also den passenden Reiter in den
        // Vordergrund holen und das Panel selbst sichtbar machen.
        MainPanel.ShowConsole();
        _mainDockService.Show(this, DockShowLocation.Bottom);

        // Welches Ziel gilt, steht im Projekt -> der passende Vorbereiter meldet sich selbst,
        // statt in einer Auswahlliste der Leiste zu stehen.
        if (_debuggerService.TargetPreparers.FirstOrDefault(CanPrepareSafely) is { } preparer)
        {
            await StartWithPreparerAsync(preparer);
            return;
        }

        StatusText = "Starting GDB...";

        var request = BuildLaunchRequest();

        // Kein Vorbereiter hat sich gemeldet -> das benennen, bevor der Notweg laeuft. Ohne diese
        // Zeile sieht ein nicht erkanntes Projekt genauso aus wie der gewollte Start, und der
        // Unterschied zeigt sich erst Sekunden spaeter als Verbindungsfehler an einem Port, den
        // niemand hochgefahren hat. Die Zahl trennt die beiden Faelle: 0 heisst, es ist gar kein
        // Plugin geladen, sonst war keines fuer dieses Projekt zustaendig.
        MainPanel.Console.Append(
            $"No target preparer is responsible for the active project " +
            $"({_debuggerService.TargetPreparers.Count} registered). Using the endpoint from " +
            $"Tools > Debugger: {request.RemoteEndpoint ?? "none"}.");

        if (!await _debuggerService.StartAsync(request))
        {
            MainPanel.Console.Append("GDB could not be started.");
            StatusText = "Start failed";
            return;
        }

        // Erst jetzt, nicht vor dem Start: sonst stuende der Hinweis noch vor der Versionszeile von
        // GDB und laese sich wie ein Abbruch, obwohl gerade nichts fehlgeschlagen ist.
        if (request.ExecutablePath == null && request.RemoteEndpoint == null)
            MainPanel.Console.Append("No executable in the active project and no remote endpoint under " +
                                     "Tools > Debugger - running without a target. Registers, memory and " +
                                     "stepping need one.");
    }

    // Startet ueber einen Vorbereiter: der bringt sein Ziel hoch und liefert die
    // Startanforderung, gestartet wird damit im Service.
    // Das Vorbereiten dauert - assemblieren, eine serielle Schnittstelle suchen, ein Programm
    // uebertragen. Solange gibt es keine Sitzung, weshalb der Startknopf ueber
    // IsPreparing gesperrt wird und nicht ueber IsSessionActive.
    // Was dabei geschieht, meldet der Vorbereiter selbst; hier steht nur, dass etwas laeuft.
    private async Task StartWithPreparerAsync(IDebugTargetPreparer preparer)
    {
        IsPreparing = true;
        StatusText = $"Preparing {preparer.DisplayName}...";

        try
        {
            if (await _debuggerService.StartAsync(preparer)) return;

            MainPanel.Console.Append($"{preparer.DisplayName} could not be started.");
            StatusText = "Start failed";
        }
        finally
        {
            IsPreparing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsSessionEstablished))]
    private Task StopAsync()
    {
        StatusText = "Stopping...";
        return _debuggerService.StopAsync();
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private Task PauseAsync()
    {
        return _debuggerService.PauseAsync();
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private Task ContinueAsync()
    {
        return _debuggerService.ContinueAsync();
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private Task StepOverAsync()
    {
        return _debuggerService.StepOverAsync();
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private Task StepIntoAsync()
    {
        return _debuggerService.StepIntoAsync();
    }

    [RelayCommand(CanExecute = nameof(CanStepOut))]
    private Task StepOutAsync()
    {
        return _debuggerService.StepOutAsync();
    }

    // Stellt zusammen, was gedebuggt werden soll: die Programmdatei aus dem aktiven Projekt und,
    // sofern eingerichtet, die Adresse des Stubs.
    // Fehlt beides, wird trotzdem gestartet. GDB laeuft dann ohne Ziel, und genau das ist der
    // Fall, in dem man es braucht: die Debugger Console beantwortet Kommandos, man kann die
    // Installation pruefen und sich von Hand an ein Ziel haengen. Den Start hier abzulehnen
    // hiesse, dem Nutzer den Weg zu verbauen, auf dem er das Problem ueberhaupt findet.
    private DebugLaunchRequest BuildLaunchRequest()
    {
        var endpoint = _settingsService.GetSettingValue<string>(DebuggerModule.RemoteEndpointSetting);
        if (string.IsNullOrWhiteSpace(endpoint)) endpoint = null;

        return new DebugLaunchRequest(GdbSessionLauncher.BackendId, FindProjectExecutable(), endpoint,
            _projectExplorerService.ActiveProject?.FullPath);
    }

    // IDebugTargetPreparer.CanPrepare kommt aus einem Plugin. Wirft es, faellt nur dieser eine
    // Vorbereiter aus, statt den Start zu verhindern.
    private bool CanPrepareSafely(IDebugTargetPreparer preparer)
    {
        try
        {
            return preparer.CanPrepare();
        }
        catch (Exception e)
        {
            _logger.Error($"Target preparer '{preparer.DisplayName}' failed in CanPrepare: {e.Message}", e);
            return false;
        }
    }

    // Die Programmdatei des aktiven Projekts, bei jedem Start neu gesucht: nach einem Neubau
    // liegt sie sonst da, wuerde aber erst nach einem Neustart des Studios gefunden.
    // Ein Ziel wie der SVNR haelt sein Programm selbst und bringt gar keine mit - dann bleibt es
    // bei null, und GDB haengt sich ohne Symbole an.
    private string? FindProjectExecutable()
    {
        if (_projectExplorerService.ActiveProject is not { } project) return null;

        try
        {
            if (DeclaredExecutable(project) is { } declared) return declared;

            return Directory.EnumerateFiles(project.FullPath, "*.elf", SearchOption.AllDirectories)
                .Order(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return null;
        }
    }

    private string? DeclaredExecutable(IProjectRoot project)
    {
        if (project is not IProjectRootWithFile declaring) return null;
        if (declaring.Properties.GetString(DebuggerModule.ExecutableProperty) is not { Length: > 0 } relative)
            return null;

        var path = Path.Combine(project.FullPath,
            relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(path)) return path;

        _logger.Warning($"The project declares '{relative}' as its debug executable, but no file exists at " +
                        $"{path} - searching the project folder for a *.elf instead.");
        return null;
    }

    // Der eine Ort, an dem ein Halt ankommt. Von hier bekommen die Reiter ihren Zustand, und
    // zwar in einem Zug -> Register und Speicher koennen nie aus verschiedenen Halts stammen.
    private void OnDebuggerStateChanged(object? sender, EventArgs e)
    {
        var wasActive = IsSessionActive;

        IsSessionActive = _debuggerService.IsActive;
        IsRunning = _debuggerService.State.IsRunning;

        if (!IsSessionActive)
        {
            if (wasActive) MainPanel.Console.Append("GDB exited.");
            DetachFromSession();
            MainPanel.Registers.Clear();
            MainPanel.Memory.ClearValues();
            StatusText = "No session";
            return;
        }

        if (!wasActive) AttachToSession();

        MainPanel.Registers.Apply(FilterRegisters(_debuggerService.State.Registers), IsRunning);
        StatusText = DescribeState(_debuggerService.State);

        MainPanel.Memory.Refresh();
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
        MainPanel.Console.Append(line);
    }


    private IReadOnlyList<DebugRegisterValue> FilterRegisters(IReadOnlyList<DebugRegisterValue> registers)
    {
        if (_debuggerService.TargetProfile.Registers is not { Count: > 0 } wanted) return registers;

        return wanted
            .Select(name => registers.FirstOrDefault(x => x.Name == name))
            .OfType<DebugRegisterValue>()
            .ToList();
    }

    private void OnCommandSent(object? sender, string command)
    {
        MainPanel.Console.Append($"> {command}", true);
    }

    private bool CanStart()
    {
        return !IsSessionActive && !IsPreparing;
    }

    private bool IsSessionEstablished()
    {
        return IsSessionActive && !IsPreparing;
    }

    private bool CanPause()
    {
        return IsSessionEstablished() && IsRunning;
    }

    private bool CanResume()
    {
        return IsSessionEstablished() && !IsRunning;
    }

    private bool CanStepOut()
    {
        return CanResume() && _debuggerService.TargetProfile.HasCallStack;
    }

    partial void OnIsPreparingChanged(bool value)
    {
        NotifyCommandStates();
    }

    partial void OnIsRunningChanged(bool value)
    {
        NotifyCommandStates();
    }

    partial void OnIsSessionActiveChanged(bool value)
    {
        // Die Kommandozeile der Konsole sperrt sich ueber denselben Zustand, haengt aber nicht
        // selbst am Dienst.
        MainPanel.Console.IsSessionActive = value;

        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ContinueCommand.NotifyCanExecuteChanged();
        StepOverCommand.NotifyCanExecuteChanged();
        StepIntoCommand.NotifyCanExecuteChanged();
        StepOutCommand.NotifyCanExecuteChanged();
    }
}
