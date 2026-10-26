using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OneWare.Debugger.ViewModels.Main;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.ProjectActions;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

// Bottom panel: the control bar of the debug session and the tab area below it.
// Everything comes together here - this is the only place that subscribes to IDebuggerService
// and attaches to the session. The tabs are handed their state from here and need not
// subscribe to anything themselves.
// Binding goes exclusively to IDebuggerService, never to a session. That way nothing has to be
// rewired on start and stop, and the panel also works when it is
// only opened while a session is running.
public partial class DebuggerViewModel : ExtendedTool
{
    public const string IconKey = "Codeicons.Debug";

    private readonly IDebuggerService _debuggerService;
    private readonly InspectorEditSession _editSession;
    private readonly ILogger _logger;
    private readonly IMainDockService _mainDockService;
    private readonly IProjectActionService _projectActionService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ISettingsService _settingsService;
    private ExtendedTextEditor? _executionLineEditor;

    [ObservableProperty] private bool _isRunning;

    // A preparer is working. There is no session yet, but starting is no longer
    // allowed all the same - IsSessionActive alone would leave the button enabled.
    [ObservableProperty] private bool _isPreparing;

    [ObservableProperty] private bool _isSessionActive;

    [ObservableProperty] private string _statusText = "No session";

    private IDebugSession? _attachedSession;

    public DebuggerViewModel(IDebuggerService debuggerService, ILogger logger, ISettingsService settingsService,
        IProjectExplorerService projectExplorerService, IMainDockService mainDockService,
        IProjectActionService projectActionService, MainPanelViewModel mainPanel, InspectorEditSession editSession) : base(IconKey)
    {
        _debuggerService = debuggerService;
        _editSession = editSession;
        _logger = logger;
        _settingsService = settingsService;
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;
        _projectActionService = projectActionService;

        // Set before subscribing -> even the first state change accesses the tabs.
        MainPanel = mainPanel;

        Id = "Debugger";
        Title = "Debugger";

        _debuggerService.StateChanged += OnDebuggerStateChanged;

        _editSession.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InspectorEditSession.IsEditing)) NotifyCommandStates();
        };
    }

    // The tab area below the bar.
    public MainPanelViewModel MainPanel { get; }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        // A project type that knows how to debug its projects (e.g. build first, then launch the exact
        // artifact) runs through the action service; otherwise start directly so the console is usable
        // even without a target.
        if (_projectActionService.CanExecute(ProjectActionKind.Debug))
        {
            await _projectActionService.ExecuteAsync(ProjectActionKind.Debug);
            return;
        }

        await StartSessionAsync();
    }

    /// <summary>
    /// Starts a session for the active project through a target preparer or with GDB directly.
    /// </summary>
    public async Task<bool> StartSessionAsync()
    {
        // On start, the traffic with the backend is what matters - so bring the matching tab to the
        // front and make the panel itself visible.
        MainPanel.ShowConsole();
        _mainDockService.Show(this, DockShowLocation.Bottom);

        // Which target applies is stated in the project -> the matching preparer reports itself,
        // instead of being listed in a selection in the bar.
        if (_debuggerService.TargetPreparers.FirstOrDefault(CanPrepareSafely) is { } preparer)
            return await StartWithPreparerAsync(preparer);

        StatusText = "Starting GDB...";

        var request = BuildLaunchRequest();

        // No preparer reported -> name that before the fallback runs. Without this
        // line an unrecognised project looks just like the intended start, and the
        // difference only shows seconds later as a connection error on a port that
        // nobody brought up. The number separates the two cases: 0 means no plugin is
        // loaded at all, otherwise none was responsible for this project.
        MainPanel.Console.AppendInfo(
            $"No target preparer is responsible for the active project " +
            $"({_debuggerService.TargetPreparers.Count} registered). Using the endpoint from " +
            $"Tools > Debugger: {request.RemoteEndpoint ?? "none"}.");

        if (!await _debuggerService.StartAsync(request))
        {
            MainPanel.Console.AppendInfo("GDB could not be started.");
            StatusText = "Start failed";
            return false;
        }

        // Only now, not before the start: otherwise the note would stand before GDB's version line
        // and read like an abort, although nothing has failed.
        if (request.ExecutablePath == null && request.RemoteEndpoint == null)
            MainPanel.Console.AppendInfo("No executable in the active project and no remote endpoint under " +
                                     "Tools > Debugger - running without a target. Registers, memory and " +
                                     "stepping need one.");

        return true;
    }

    /// <summary>
    /// Starts a session with a launch request built by a project type, e.g. after it built the program.
    /// </summary>
    public async Task<bool> StartSessionAsync(DebugLaunchRequest request)
    {
        MainPanel.ShowConsole();
        _mainDockService.Show(this, DockShowLocation.Bottom);

        StatusText = "Starting GDB...";

        if (await _debuggerService.StartAsync(request)) return true;

        MainPanel.Console.AppendInfo("GDB could not be started.");
        StatusText = "Start failed";
        return false;
    }

    // Starts through a preparer: it brings up its target and supplies the
    // launch request, which the service then starts with.
    // Preparing takes time - assembling, looking for a serial port, transferring a
    // program. Until then there is no session, which is why the start button is locked through
    // IsPreparing and not through IsSessionActive.
    // What happens meanwhile is reported by the preparer itself; here it only says that something runs.
    private async Task<bool> StartWithPreparerAsync(IDebugTargetPreparer preparer)
    {
        IsPreparing = true;
        StatusText = $"Preparing {preparer.DisplayName}...";

        try
        {
            if (await _debuggerService.StartAsync(preparer)) return true;

            MainPanel.Console.AppendInfo($"{preparer.DisplayName} could not be started.");
            StatusText = "Start failed";
            return false;
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

    // Puts together what is to be debugged: the program file from the active project and,
    // if configured, the address of the stub.
    // If both are missing, it starts anyway. GDB then runs without a target, and that is exactly the
    // case in which it is needed: the debugger console answers commands, the installation can be
    // checked and one can attach to a target by hand. Refusing the start here
    // would block the very path on which the user finds the problem.
    private DebugLaunchRequest BuildLaunchRequest()
    {
        var endpoint = _settingsService.GetSettingValue<string>(DebuggerModule.RemoteEndpointSetting);
        if (string.IsNullOrWhiteSpace(endpoint)) endpoint = null;

        return new DebugLaunchRequest(GdbSessionLauncher.BackendId, FindProjectExecutable(), endpoint,
            _projectExplorerService.ActiveProject?.FullPath);
    }

    // IDebugTargetPreparer.CanPrepare comes from a plugin. If it throws, only this one
    // preparer drops out, instead of preventing the start.
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

    // The program file of the active project, searched anew on every start: after a rebuild
    // it would otherwise be there but only be found after restarting the studio.
    // A target that holds its program itself brings none along - then it stays
    // null, and GDB attaches without symbols.
    private string? FindProjectExecutable()
    {
        if (_projectExplorerService.ActiveProject is not { } project) return null;

        // The exact file from the last build wins over any guess.
        if (_projectActionService.GetArtifacts(project)
                .FirstOrDefault(x => x.Kind is BuildArtifactKind.Executable or BuildArtifactKind.Elf
                                     && File.Exists(x.Path)) is { } artifact)
            return artifact.Path;

        try
        {
            if (DeclaredExecutable(project) is { } declared) return declared;
            var executableExtensions = new[] { ".elf", ".axf", ".out", ".exe", ".dll", ".dylib",};
            return executableExtensions
                .SelectMany(extension =>
                    Directory.EnumerateFiles(
                        project.FullPath,
                        $"*{extension}",
                        SearchOption.AllDirectories))
                .Order(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.Error($"Failed to find executable in project '{project.FullPath}': {e.Message}", e);
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
                        $"{path} - searching the project folder for an executable with debug info instead.");
        return null;
    }

    // The one place where a halt arrives. The tabs get their state from here, and
    // in one go -> registers and memory can never come from different halts.
    private void OnDebuggerStateChanged(object? sender, EventArgs e)
    {
        var wasActive = IsSessionActive;

        IsSessionActive = _debuggerService.IsActive;
        IsRunning = _debuggerService.State.IsRunning;

        if (!IsSessionActive)
        {
            if (wasActive) MainPanel.Console.AppendInfo("GDB exited.");
            DetachFromSession();
            ClearExecutionLine();
            MainPanel.Registers.Clear();
            MainPanel.Memory.ClearValues();
            StatusText = "No session";
            return;
        }

        if (!wasActive) AttachToSession();

        MainPanel.Registers.Apply(FilterRegisters(_debuggerService.State.Registers), IsRunning,
            _debuggerService.TargetProfile.ReadOnlyRegisters);
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

        // Without symbols the program counter is the only thing that describes the halt.
        return frame.Address is { } address ? $"Stopped at {address}" : "Stopped";
    }

    private async Task JumpToCurrentFrameAsync(DebugSessionState state)
    {
        if (state.IsRunning || state.CurrentFrame is not { File: { } file, Line: > 0 } frame || !File.Exists(file))
        {
            ClearExecutionLine();
            return;
        }

        try
        {
            if (await _mainDockService.OpenFileAsync(file) is not IEditor editor) return;

            // The halt may already be over while the file was opening.
            if (!ReferenceEquals(_debuggerService.State, state)) return;

            if (!ReferenceEquals(_executionLineEditor, editor.Editor)) ClearExecutionLine();

            // No selection: the execution line has its own marker, independent of what the user selects.
            editor.JumpToLine(frame.Line, false);
            editor.Editor.ExecutionLineRenderer.Line = frame.Line;
            _executionLineEditor = editor.Editor;
        }
        catch (Exception e)
        {
            _logger.Error($"Could not jump to '{file}' line {frame.Line}: {e.Message}", e);
        }
    }

    private void ClearExecutionLine()
    {
        if (_executionLineEditor == null) return;

        _executionLineEditor.ExecutionLineRenderer.Line = null;
        _executionLineEditor = null;
    }

    private void OnOutputReceived(object? sender, string line)
    {
        MainPanel.Console.AppendOutput(line);
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
        MainPanel.Console.AppendCommand(command);
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
        return IsSessionEstablished() && !IsRunning && !_editSession.IsEditing;
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
        // The console's command line locks through the same state, but is not attached to the
        // service itself.
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
