using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Terminal.Provider;
using OneWare.Terminal.ViewModels;
using OneWare.TerminalManager.Models;

namespace OneWare.TerminalManager.ViewModels;

public class TerminalManagerViewModel : ExtendedTool, ITerminalManagerService
{
    public const string IconKey = "Material.Console";

    // After a command reports completion, wait briefly for another command-start event.
    // Multi-line command blocks execute line by line (already buffered in the pty), so the
    // next line's start marker arrives within milliseconds; only the final completion is
    // followed by silence.
    private static readonly TimeSpan MultiCommandGracePeriod = TimeSpan.FromMilliseconds(250);

    // Automation terminals are pooled per id so that concurrent commands (e.g. an AI agent
    // running several shell commands at once) each get their own terminal tab instead of
    // interleaving on a single shell. Idle terminals in a pool are reused for sequential
    // commands so shell state (working directory, environment, ...) is preserved.
    private readonly object _automationLock = new();
    private readonly Dictionary<string, List<TerminalTabModel>> _automationPools = new(StringComparer.Ordinal);
    private readonly HashSet<TerminalViewModel> _busyAutomationTerminals = new();
    private readonly IMainDockService _mainDockService;
    private readonly IPaths _paths;

    private readonly IProjectExplorerService _projectExplorerService;

    private TerminalTabModel? _selectedTerminalTab;

    public TerminalManagerViewModel(ISettingsService settingsService, IMainDockService mainDockService,
        IProjectExplorerService projectExplorerService, IPaths paths) : base(IconKey)
    {
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;
        _paths = paths;

        Title = "Terminal";
        Id = "Terminal";

        settingsService.GetSettingObservable<string>("General_SelectedTheme").Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(5))
            .Subscribe(x => Dispatcher.UIThread.Post(() =>
            {
                foreach (var t in Terminals) t.Terminal.Redraw();
            }));
    }

    public ObservableCollection<TerminalTabModel> Terminals { get; } = new();

    public TerminalTabModel? SelectedTerminalTab
    {
        get => _selectedTerminalTab;
        set => SetProperty(ref _selectedTerminalTab, value);
    }

    public override void InitializeContent()
    {
        base.InitializeContent();
        NewTerminal();
    }

    public override void OnSelected()
    {
        base.OnSelected();
        if (!Terminals.Any()) NewTerminal();
    }

    public void CloseTab(TerminalTabModel tab)
    {
        Terminals.Remove(tab);
        RemoveAutomationTerminal(tab);

        if (!Terminals.Any())
        {
            _mainDockService.CloseDockable(this);
            return;
        }
    }

    public void NewTerminal()
    {
        NewTerminal("Local");
    }
    
    public TerminalTabModel NewTerminal(string name, string? workingDirectory = null, bool select = true)
    {
        var homeFolder = _projectExplorerService.ActiveProject?.FullPath;

        homeFolder ??= workingDirectory ?? _paths.ProjectsDirectory;
        
        var title = GetUniqueTitle(name);

        var tab = new TerminalTabModel(title, new TerminalViewModel(homeFolder), this);
        Terminals.Add(tab);

        if (select) SelectedTerminalTab = tab;

        return tab;
    }

    public async Task<TerminalExecutionResult> ExecuteInTerminalAsync(TerminalViewModel terminal, string command,
        TimeSpan? timeout = null, bool closeWhenDone = true, IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default)
    {
        var readyTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnReady(object? sender, EventArgs args) => readyTcs.TrySetResult();

        if (terminal.Connection?.IsConnected == true && !terminal.TerminalLoading)
        {
            readyTcs.TrySetResult();
        }
        else
        {
            terminal.TerminalReady += OnReady;
            terminal.CreateConnection();
        }

        try
        {
            await WaitForReadyAsync(readyTcs.Task, timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            terminal.TerminalReady -= OnReady;
            if (closeWhenDone) terminal.Close();
            return new TerminalExecutionResult(string.Empty, -1, true);
        }

        terminal.TerminalReady -= OnReady;

        if (terminal.Connection is not PseudoTerminalConnection connection)
        {
            if (closeWhenDone) terminal.Close();
            return new TerminalExecutionResult(string.Empty, -1, true);
        }

        var output = new StringBuilder();
        var stateLock = new object();
        var resultTcs =
            new TaskCompletionSource<TerminalExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandSent = false;
        // True between a command-start (OSC 633;C) and command-complete (OSC 633;D) marker,
        // i.e. while the terminal output belongs to the command we sent. Prompt drawing,
        // command echo and integration startup noise all happen outside that window.
        var capturing = false;
        var lastExitCode = 0;
        CancellationTokenSource? graceCts = null;
        // Strips echoed user keystrokes from the captured output only (not the visible terminal).
        var echoFilter = new UserInputEchoFilter();

        void CompleteWithResult()
        {
            string finalOutput;
            int exitCode;
            lock (stateLock)
            {
                finalOutput = output.ToString();
                exitCode = lastExitCode;
            }

            resultTcs.TrySetResult(new TerminalExecutionResult(finalOutput, exitCode, false));
        }

        async Task CompleteAfterGraceAsync(CancellationToken graceToken)
        {
            try
            {
                await Task.Delay(MultiCommandGracePeriod, graceToken);
            }
            catch (OperationCanceledException)
            {
                return; // Another command line started; keep waiting for its completion.
            }

            CompleteWithResult();
        }

        void OnConnectionClosed(object? sender, EventArgs args)
        {
            string partialOutput;
            lock (stateLock)
                partialOutput = output.ToString();

            resultTcs.TrySetResult(new TerminalExecutionResult(partialOutput, -1, true));
        }

        void OnDataSent(object? sender, VtNetCore.Avalonia.DataReceivedEventArgs args)
        {
            lock (stateLock)
            {
                // Input sent while our command runs is user input typed into the terminal;
                // remember it so its echo can be removed from the captured output.
                if (capturing)
                    echoFilter.OnUserInput(args.Data);
            }
        }

        void OnDataReceived(object? sender, VtNetCore.Avalonia.DataReceivedEventArgs args)
        {
            string? current = null;

            lock (stateLock)
            {
                if (!capturing) return;
                var filtered = echoFilter.Filter(args.Data);
                if (filtered.Length == 0) return;
                output.Append(Encoding.UTF8.GetString(filtered));
                current = output.ToString();
            }

            if (!resultTcs.Task.IsCompleted)
                outputProgress?.Report(current);
        }

        void OnIntegrationEvent(object? sender, ShellIntegrationEventArgs args)
        {
            lock (stateLock)
            {
                if (args.IsCommandStarted)
                {
                    capturing = true;
                    graceCts?.Cancel();
                    graceCts = null;
                }
                else if (args.IsCommandCompleted && capturing)
                {
                    // Completion markers arriving before any command started (e.g. the
                    // shell's very first prompt or the user pressing enter on an empty
                    // prompt) do not belong to our command and are ignored.
                    capturing = false;
                    lastExitCode = args.ExitCode;
                    graceCts?.Cancel();
                    graceCts = new CancellationTokenSource();
                    _ = CompleteAfterGraceAsync(graceCts.Token);
                }
            }
        }

        connection.DataReceived += OnDataReceived;
        connection.DataSent += OnDataSent;
        connection.Closed += OnConnectionClosed;
        connection.IntegrationEvent += OnIntegrationEvent;

        if (!resultTcs.Task.IsCompleted && !cancellationToken.IsCancellationRequested)
        {
            commandSent = true;
            terminal.Send(command);
        }

        TerminalExecutionResult result;

        try
        {
            result = await WaitForResultAsync(resultTcs.Task, timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            string partialOutput;
            lock (stateLock)
                partialOutput = output.ToString();

            if (commandSent)
            {
                // The command exceeded its timeout or was cancelled but is still running
                // in the shell. First try a gentle interrupt (Ctrl+C) so the shell returns
                // to a usable prompt and the terminal stays reusable.
                var recovered = await TryRecoverPromptAsync(terminal, resultTcs.Task);
                if (!recovered)
                {
                    // The interrupt did not free the shell (the process ignores SIGINT or is
                    // itself hung). Forcibly kill the process tree and discard this terminal
                    // so it is never reused in a stuck state by a subsequent command.
                    terminal.KillProcess();
                    DiscardAutomationTerminal(terminal);
                }
            }

            result = new TerminalExecutionResult(partialOutput, -1, true);
        }
        finally
        {
            lock (stateLock)
            {
                graceCts?.Cancel();
                graceCts = null;
            }

            connection.DataReceived -= OnDataReceived;
            connection.DataSent -= OnDataSent;
            connection.Closed -= OnConnectionClosed;
            connection.IntegrationEvent -= OnIntegrationEvent;
            if (closeWhenDone) terminal.Close();
        }

        return result;
    }

    private static async Task<bool> TryRecoverPromptAsync(TerminalViewModel terminal,
        Task<TerminalExecutionResult> resultTask)
    {
        terminal.SendInterrupt();
        try
        {
            // The interrupt makes the shell print a fresh prompt, whose integration
            // marker completes the pending result and keeps the terminal reusable.
            await resultTask.WaitAsync(TimeSpan.FromSeconds(3));
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private void DiscardAutomationTerminal(TerminalViewModel terminal)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var tab = Terminals.FirstOrDefault(t => ReferenceEquals(t.Terminal, terminal));
            tab?.Close();
        });
    }

    public void ExecScriptInTerminal(string scriptPath, bool elevated, string title)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotImplementedException();

            PlatformHelper.ExecBash("chmod u+x " + scriptPath);

            var sudo = elevated ? "sudo " : "";
            var terminal = new TerminalViewModel(_paths.DocumentsDirectory);

            var wrapper = new StandaloneTerminalViewModel(title, terminal);

            _mainDockService.Show(wrapper);

            Observable.FromEventPattern(terminal, nameof(terminal.TerminalReady)).Take(1)
                .Delay(TimeSpan.FromMilliseconds(100)).Subscribe(x => { terminal.Send($"{sudo}{scriptPath}"); });
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    private static async Task WaitForReadyAsync(Task readyTask, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (timeout == null)
        {
            await readyTask.WaitAsync(cancellationToken);
            return;
        }

        using var timeoutCts = new CancellationTokenSource(timeout.Value);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        await readyTask.WaitAsync(linkedCts.Token);
    }

    private static async Task<TerminalExecutionResult> WaitForResultAsync(
        Task<TerminalExecutionResult> resultTask, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (timeout == null) return await resultTask.WaitAsync(cancellationToken);

        using var timeoutCts = new CancellationTokenSource(timeout.Value);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        return await resultTask.WaitAsync(linkedCts.Token);
    }

    public async Task<TerminalExecutionResult> ExecuteInTerminalAsync(string command, string id,
        string? workingDirectory = null, bool showInUi = false, TimeSpan? timeout = null,
        IProgress<string>? outputProgress = null, CancellationToken cancellationToken = default)
    {
        if (showInUi)
            _mainDockService.Show<ITerminalManagerService>();

        var tab = AcquireAutomationTab(id, workingDirectory, showInUi);
        try
        {
            return await ExecuteInTerminalAsync(tab.Terminal, command, timeout, closeWhenDone: false, outputProgress,
                cancellationToken);
        }
        finally
        {
            ReleaseAutomationTab(tab);
        }
    }

    [Obsolete("Use the overload that accepts an IProgress<string> outputProgress parameter. " +
              "This overload is kept for plugin binary compatibility and will be removed in a future release.")]
    public Task<TerminalExecutionResult> ExecuteInTerminalAsync(string command, string id,
        string? workingDirectory, bool showInUi, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        return ExecuteInTerminalAsync(command, id, workingDirectory, showInUi, timeout, null, cancellationToken);
    }

    private TerminalTabModel AcquireAutomationTab(string id, string? workingDirectory, bool select)
    {
        lock (_automationLock)
        {
            if (!_automationPools.TryGetValue(id, out var pool))
            {
                pool = new List<TerminalTabModel>();
                _automationPools[id] = pool;
            }

            // Reuse an idle terminal from the pool so sequential commands keep their shell state.
            var idle = pool.FirstOrDefault(t => !_busyAutomationTerminals.Contains(t.Terminal));
            if (idle != null)
            {
                _busyAutomationTerminals.Add(idle.Terminal);
                if (select) SelectedTerminalTab = idle;
                return idle;
            }

            // Every pooled terminal is currently busy (or none exist yet): open another tab so
            // concurrent commands run side by side instead of colliding on one shell.
            var tab = NewTerminal(id, workingDirectory, select);
            pool.Add(tab);
            _busyAutomationTerminals.Add(tab.Terminal);
            return tab;
        }
    }

    private void ReleaseAutomationTab(TerminalTabModel tab)
    {
        lock (_automationLock)
        {
            _busyAutomationTerminals.Remove(tab.Terminal);
        }
    }

    private void RemoveAutomationTerminal(TerminalTabModel tab)
    {
        lock (_automationLock)
        {
            _busyAutomationTerminals.Remove(tab.Terminal);

            foreach (var pool in _automationPools.Values)
                pool.Remove(tab);
        }
    }

    private string GetUniqueTitle(string baseName)
    {
        var hasBase = false;
        var maxNumber = 0;

        foreach (var terminal in Terminals)
        {
            if (terminal.Title == baseName)
            {
                hasBase = true;
                continue;
            }

            if (!terminal.Title.StartsWith($"{baseName} (", StringComparison.Ordinal) ||
                !terminal.Title.EndsWith(")", StringComparison.Ordinal))
            {
                continue;
            }

            var numberSpan = terminal.Title.AsSpan(baseName.Length + 2, terminal.Title.Length - baseName.Length - 3);
            if (int.TryParse(numberSpan, out var number))
            {
                if (number > maxNumber) maxNumber = number;
            }
        }

        if (!hasBase) return baseName;

        var nextNumber = Math.Max(1, maxNumber + 1);
        return $"{baseName} ({nextNumber})";
    }
}
