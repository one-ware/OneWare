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

    // The ready handshake is a purely local operation. It must never inherit the (possibly
    // very long) command timeout: a shell that fails to spawn would otherwise block the
    // caller for hours instead of failing fast.
    private static readonly TimeSpan TerminalStartTimeout = TimeSpan.FromSeconds(30);

    // Time granted to a freshly started shell to emit its first prompt marker. Sending the
    // command before the integration hooks are installed loses that command's lifecycle
    // markers, which is the classic cause of an execution that never returns.
    private static readonly TimeSpan ShellIntegrationProbeTimeout = TimeSpan.FromSeconds(10);

    // Fallback for shells whose integration never works (unknown shell, blocked startup
    // files, ...): the command is considered finished once its output stays silent this
    // long. Without markers there is no better completion signal, and waiting forever is
    // never an acceptable outcome.
    private static readonly TimeSpan NoIntegrationIdleTimeout = TimeSpan.FromSeconds(5);

    // Per attempt time granted to Ctrl+C before the process tree is killed.
    private static readonly TimeSpan InterruptRecoveryTimeout = TimeSpan.FromSeconds(2);
    private const int InterruptAttempts = 2;

    // Progress is pushed to the UI thread, where it re-renders the whole captured text.
    // Reporting every pty chunk turns a chatty command into an application freeze, so
    // updates are rate limited.
    private const long ProgressReportIntervalMs = 250;

    // Hard cap on captured output so a runaway command cannot exhaust memory.
    private const int MaxCapturedOutputChars = 1_000_000;

    // Applied when the caller passes no timeout. A command whose completion marker never
    // arrives (a nested shell, an interactive REPL, ...) would otherwise block its caller
    // forever; automation must always terminate.
    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromHours(1);

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

    // Set once a shell failed to report any integration marker. The shell is chosen per
    // platform, so the result applies to every terminal and spares later commands the probe.
    private volatile bool _shellIntegrationUnavailable;

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
        // Close the tab when its shell exits (e.g. the user or an automation command runs "exit").
        tab.Terminal.ConnectionClosed += (_, _) => Dispatcher.UIThread.Post(tab.Close);
        Terminals.Add(tab);

        if (select) SelectedTerminalTab = tab;

        return tab;
    }

    public async Task<TerminalExecutionResult> ExecuteInTerminalAsync(TerminalViewModel terminal, string command,
        TimeSpan? timeout = null, bool closeWhenDone = true, IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default)
    {
        PseudoTerminalConnection? connection;
        try
        {
            connection = await EnsureConnectedAsync(terminal, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (closeWhenDone) terminal.Close();
            return new TerminalExecutionResult(string.Empty, -1, true);
        }

        if (connection == null)
        {
            // The shell could not be started (or did not become ready in time). Drop the
            // terminal so the next command gets a fresh one instead of retrying a dead tab.
            if (closeWhenDone) terminal.Close();
            else DiscardAutomationTerminal(terminal);
            return new TerminalExecutionResult("[terminal could not be started]", -1, true);
        }

        var output = new StringBuilder();
        var stateLock = new object();
        var resultTcs =
            new TaskCompletionSource<TerminalExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var integrationTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Strips echoed keystrokes (ours and the user's) from the captured output only.
        var echoFilter = new UserInputEchoFilter();
        var idleCts = new CancellationTokenSource();

        var commandSent = false;
        // True from the moment the command is written to the pty. Capturing deliberately does
        // NOT wait for the command-start marker: that marker is the least reliable part of
        // shell integration (it depends on PSReadLine on Windows), and gating capture and
        // completion on it made a missing marker hang the execution forever.
        var capturing = false;
        var sawCommandStart = false;
        var integrationSeen = connection.ShellIntegrationDetected;
        var lastExitCode = 0;
        var exitCodeKnown = false;
        var lastActivityTicks = Environment.TickCount64;
        var lastProgressTicks = 0L;
        var idleFallbackUsed = false;
        CancellationTokenSource? graceCts = null;

        void ReplaceGraceSource(CancellationTokenSource? replacement)
        {
            var previous = graceCts;
            graceCts = replacement;
            previous?.Cancel();
            previous?.Dispose();
        }

        void AppendOutput(string text)
        {
            output.Append(text);
            if (output.Length > MaxCapturedOutputChars)
                output.Remove(0, output.Length - MaxCapturedOutputChars);
        }

        void CompleteWithResult(bool exitCodeIsKnown)
        {
            string finalOutput;
            int exitCode;
            lock (stateLock)
            {
                finalOutput = output.ToString();
                exitCode = lastExitCode;
            }

            if (!exitCodeIsKnown)
                finalOutput +=
                    "\n[no shell integration: the command was assumed to have finished after its output " +
                    "went idle, the exit code is unknown]";

            // An unknown exit code must never look like success: -1 is the established
            // "indeterminate" value of this result type.
            resultTcs.TrySetResult(new TerminalExecutionResult(finalOutput, exitCodeIsKnown ? exitCode : -1, false));
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

            CompleteWithResult(true);
        }

        // Only used while the shell reports no lifecycle markers at all. Completing on
        // silence is a guess, but it is the only way to guarantee that the call returns.
        async Task WatchIdleAsync(CancellationToken idleToken)
        {
            while (!idleToken.IsCancellationRequested)
            {
                TimeSpan remaining;
                lock (stateLock)
                {
                    if (integrationSeen) return;
                    var idleFor = TimeSpan.FromMilliseconds(Environment.TickCount64 - lastActivityTicks);
                    remaining = NoIntegrationIdleTimeout - idleFor;
                }

                if (remaining <= TimeSpan.Zero)
                {
                    // The command may well still be running (it was only guessed to have
                    // finished), so the shell must not be handed to the next command.
                    idleFallbackUsed = true;
                    CompleteWithResult(false);
                    return;
                }

                try
                {
                    await Task.Delay(remaining, idleToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        void OnConnectionClosed(object? sender, EventArgs args)
        {
            string partialOutput;
            int exitCode;
            lock (stateLock)
            {
                partialOutput = output.ToString();
                exitCode = capturing && !exitCodeKnown ? -1 : lastExitCode;
            }

            // The shell itself exited (e.g. the command was "exit 3"). Prefer the real
            // process exit code over the marker-based one, which can never arrive.
            if ((sender as PseudoTerminalConnection)?.ProcessExitCode is { } processExitCode)
                exitCode = processExitCode;

            resultTcs.TrySetResult(new TerminalExecutionResult(
                partialOutput + "\n[terminal session ended]", exitCode, false));
        }

        void OnDataSent(object? sender, VtNetCore.Avalonia.DataReceivedEventArgs args)
        {
            lock (stateLock)
            {
                // Input written while our command runs (the command line itself and anything
                // the user types into the terminal) is echoed back by the pty; remember it so
                // the echo can be removed from the captured output.
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

                lastActivityTicks = Environment.TickCount64;

                var filtered = echoFilter.Filter(args.Data);
                if (filtered.Length == 0) return;
                AppendOutput(Encoding.UTF8.GetString(filtered));

                var now = Environment.TickCount64;
                if (outputProgress != null && now - lastProgressTicks >= ProgressReportIntervalMs)
                {
                    lastProgressTicks = now;
                    current = output.ToString();
                }
            }

            if (current != null && !resultTcs.Task.IsCompleted)
                outputProgress?.Report(current);
        }

        void OnIntegrationEvent(object? sender, ShellIntegrationEventArgs args)
        {
            lock (stateLock)
            {
                integrationSeen = true;
                integrationTcs.TrySetResult();
                lastActivityTicks = Environment.TickCount64;

                if (!commandSent) return;

                if (args.IsCommandStarted)
                {
                    // The shell confirmed our command started. Everything captured so far is
                    // prompt redraw and command echo, which the echo filter cannot always
                    // recognize (PSReadLine re-renders the line with colors), so drop it.
                    if (!sawCommandStart)
                    {
                        sawCommandStart = true;
                        output.Clear();
                        echoFilter.Reset();
                    }

                    capturing = true;
                    ReplaceGraceSource(null);
                }
                else if (args.IsCommandCompleted)
                {
                    lastExitCode = args.ExitCode;
                    exitCodeKnown = true;
                    // Stop capturing so the prompt drawn right after the command does not
                    // leak into the output. Only safe when the shell also emits command-start
                    // markers, because those are what resume capture for the next line of a
                    // multi-line command block.
                    if (sawCommandStart) capturing = false;
                    var grace = new CancellationTokenSource();
                    ReplaceGraceSource(grace);
                    _ = CompleteAfterGraceAsync(grace.Token);
                }
            }
        }

        connection.DataReceived += OnDataReceived;
        connection.DataSent += OnDataSent;
        connection.Closed += OnConnectionClosed;
        connection.IntegrationEvent += OnIntegrationEvent;

        TerminalExecutionResult result;

        try
        {
            // Wait until the shell reached its first prompt. That proves the integration
            // hooks are installed, so the markers of the command we are about to send cannot
            // be missed. Skipping this step is what made executions hang indefinitely.
            if (!integrationSeen && !connection.ShellIntegrationProbeFailed && !_shellIntegrationUnavailable)
            {
                try
                {
                    await integrationTcs.Task.WaitAsync(ShellIntegrationProbeTimeout, cancellationToken);
                }
                catch (TimeoutException)
                {
                    // This shell has no working integration. Remember it so following commands
                    // do not pay the probe timeout again; the shell is a property of the
                    // platform, so the result applies to every terminal of this manager.
                    connection.ShellIntegrationProbeFailed = true;
                    _shellIntegrationUnavailable = true;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            bool useIdleFallback;
            lock (stateLock)
            {
                capturing = true;
                commandSent = true;
                lastActivityTicks = Environment.TickCount64;
                useIdleFallback = !integrationSeen;
            }

            if (useIdleFallback) _ = WatchIdleAsync(idleCts.Token);

            terminal.Send(command);

            result = await WaitForResultAsync(resultTcs.Task, timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (commandSent)
            {
                // The command exceeded its timeout or was cancelled but is still running in
                // the shell. First try a gentle interrupt (Ctrl+C) so the shell returns to a
                // usable prompt and the terminal stays reusable.
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

            string partialOutput;
            lock (stateLock)
                partialOutput = output.ToString();

            result = new TerminalExecutionResult(partialOutput, -1, true);
        }
        finally
        {
            lock (stateLock)
            {
                ReplaceGraceSource(null);
            }

            idleCts.Cancel();
            idleCts.Dispose();

            if (idleFallbackUsed)
            {
                // The command was only *assumed* to be finished. It may still be attached to
                // this shell, so the shell must never serve another command: its output would
                // be mixed into the next result and the next command line would be fed to the
                // still running process as stdin.
                terminal.KillProcess();
                DiscardAutomationTerminal(terminal);
                terminal.Close();
            }

            connection.DataReceived -= OnDataReceived;
            connection.DataSent -= OnDataSent;
            connection.Closed -= OnConnectionClosed;
            connection.IntegrationEvent -= OnIntegrationEvent;
            if (closeWhenDone) terminal.Close();
        }

        return result;
    }

    /// <summary>
    /// Brings the terminal up to a connected state and returns its pty connection, or null
    /// when the shell could not be started.
    /// </summary>
    private static async Task<PseudoTerminalConnection?> EnsureConnectedAsync(TerminalViewModel terminal,
        CancellationToken cancellationToken)
    {
        var readyTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnReady(object? sender, EventArgs args) => readyTcs.TrySetResult();

        terminal.TerminalReady += OnReady;

        try
        {
            if (terminal.Connection is { IsConnected: true } && !terminal.TerminalLoading)
                readyTcs.TrySetResult();
            else
                terminal.CreateConnection();

            await readyTcs.Task.WaitAsync(TerminalStartTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return null;
        }
        finally
        {
            terminal.TerminalReady -= OnReady;
        }

        return terminal.Connection as PseudoTerminalConnection;
    }

    private static async Task<bool> TryRecoverPromptAsync(TerminalViewModel terminal,
        Task<TerminalExecutionResult> resultTask)
    {
        for (var attempt = 0; attempt < InterruptAttempts; attempt++)
        {
            terminal.SendInterrupt();
            try
            {
                // The interrupt makes the shell print a fresh prompt, whose integration
                // marker completes the pending result and keeps the terminal reusable.
                await resultTask.WaitAsync(InterruptRecoveryTimeout);
                return true;
            }
            catch (TimeoutException)
            {
                // Retry once: the first Ctrl+C is sometimes swallowed by a program that
                // installs its own handler while starting up.
            }
        }

        return false;
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

    private static async Task<TerminalExecutionResult> WaitForResultAsync(
        Task<TerminalExecutionResult> resultTask, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        // Never wait unbounded: a command whose completion marker never arrives would
        // otherwise block the caller for the lifetime of the application.
        using var timeoutCts = new CancellationTokenSource(timeout ?? DefaultCommandTimeout);
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
