using System.Collections.ObjectModel;
using System.Diagnostics;
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
using OneWare.Terminal.ViewModels;
using OneWare.TerminalManager.Models;

namespace OneWare.TerminalManager.ViewModels;

public class TerminalManagerViewModel : ExtendedTool, ITerminalManagerService
{
    public const string IconKey = "Material.Console";
    private const string PromptMarkerPrefix = "\u001b]9;OW_DONE:";

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

        if (terminal.Connection == null)
        {
            if (closeWhenDone) terminal.Close();
            return new TerminalExecutionResult(string.Empty, -1, true);
        }

        var output = new StringBuilder();
        var outputLock = new object();
        var resultTcs = new TaskCompletionSource<TerminalExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandSent = false;
        var lastOutputTimestamp = Stopwatch.GetTimestamp();
        var markerPrefix = PromptMarkerPrefix;
        var commandToSend = command;
        var commandEchoPrefix = GetCommandEchoPrefix(command);
        string? markerCommand = null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var executionId = Guid.NewGuid().ToString("N");
            markerPrefix = $"{PromptMarkerPrefix}{executionId}:";

            // Do not depend on PROMPT_COMMAND/precmd for automation completion. User shell
            // configuration can replace those hooks, leaving the shell visibly idle while the
            // caller waits forever. Appending a per-command marker also prevents startup or
            // unrelated prompt markers from completing the wrong invocation.
            markerCommand =
                $"__ow_exit=$?; printf '\\033[1A\\r\\033[2K\\033]9;OW_DONE:{executionId}:%s\\007' \"$__ow_exit\"";
            commandToSend = $"{command}\n{markerCommand}";
        }

        void OnConnectionClosed(object? sender, EventArgs args)
        {
            string partialOutput;
            lock (outputLock)
                partialOutput = output.ToString();

            resultTcs.TrySetResult(new TerminalExecutionResult(partialOutput, -1, true));
        }

        void OnDataReceived(object? sender, VtNetCore.Avalonia.DataReceivedEventArgs args)
        {
            Interlocked.Exchange(ref lastOutputTimestamp, Stopwatch.GetTimestamp());
            var text = Encoding.UTF8.GetString(args.Data);
            string current;
            int? completedExitCode = null;

            lock (outputLock)
            {
                output.Append(text);
                current = output.ToString();

                var markerFound = TryExtractOscMarker(current, markerPrefix, out var exitCode, out var cleaned);
                var commandEchoIndex = commandEchoPrefix.Length == 0
                    ? -1
                    : current.IndexOf(commandEchoPrefix, StringComparison.Ordinal);
                if (!markerFound && markerPrefix != PromptMarkerPrefix && commandEchoIndex >= 0)
                {
                    // The shell's prompt hook is an independent completion signal. Prefer the
                    // invocation-specific marker. Only consider prompt markers that occur after
                    // this command's echo so a late marker from a pooled terminal cannot complete
                    // the wrong invocation.
                    markerFound = TryExtractOscMarker(current, PromptMarkerPrefix, out exitCode, out cleaned,
                        commandEchoIndex + commandEchoPrefix.Length);
                }

                if (markerFound)
                {
                    while (TryExtractOscMarker(cleaned, PromptMarkerPrefix, out _, out var promptCleaned))
                        cleaned = promptCleaned;

                    current = cleaned;
                    output.Clear();
                    output.Append(cleaned);

                    if (commandSent)
                        completedExitCode = exitCode;
                }
            }

            if (completedExitCode is { } exitCodeResult)
            {
                outputProgress?.Report(current);
                resultTcs.TrySetResult(new TerminalExecutionResult(current, exitCodeResult, false));
                return;
            }

            if (commandSent && !resultTcs.Task.IsCompleted)
                outputProgress?.Report(current);
        }

        terminal.Connection.DataReceived += OnDataReceived;
        terminal.Connection.Closed += OnConnectionClosed;

        if (markerCommand != null)
        {
            // The shell echoes queued input before executing it. Hide the internal marker
            // command independently of the PTY's line-ending mode. The marker then moves back
            // and clears the intermediate prompt before the final prompt is rendered.
            terminal.SuppressEcho(Encoding.UTF8.GetBytes(markerCommand));
        }

        if (!resultTcs.Task.IsCompleted && !cancellationToken.IsCancellationRequested)
        {
            commandSent = true;
            terminal.Send(commandToSend);
        }

        TerminalExecutionResult result;
        using var completionProbeCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var completionProbeTask = markerCommand == null
            ? Task.CompletedTask
            : ProbeForMissedCompletionAsync(
                terminal,
                markerCommand,
                resultTcs.Task,
                () => Volatile.Read(ref lastOutputTimestamp),
                completionProbeCancellation.Token);

        try
        {
            result = await WaitForResultAsync(resultTcs.Task, timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            string partialOutput;
            lock (outputLock)
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
            await completionProbeCancellation.CancelAsync();
            await completionProbeTask;
            terminal.Connection.DataReceived -= OnDataReceived;
            terminal.Connection.Closed -= OnConnectionClosed;
            if (closeWhenDone) terminal.Close();
        }

        return result;
    }

    private static async Task ProbeForMissedCompletionAsync(
        TerminalViewModel terminal,
        string markerCommand,
        Task<TerminalExecutionResult> resultTask,
        Func<long> getLastOutputTimestamp,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!resultTask.IsCompleted)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                if (resultTask.IsCompleted) return;
                if (Stopwatch.GetElapsedTime(getLastOutputTimestamp()) < TimeSpan.FromSeconds(1)) continue;
                if (terminal.IsShellForeground != true) continue;

                // The command has returned control to the shell but its queued completion marker
                // was not observed. Re-send the same marker once; if a shell builtin is still
                // executing, the terminal queues it until that builtin returns.
                terminal.SuppressEcho(Encoding.UTF8.GetBytes(markerCommand));
                terminal.Send(markerCommand);
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task<bool> TryRecoverPromptAsync(TerminalViewModel terminal,
        Task<TerminalExecutionResult> resultTask)
    {
        terminal.SendInterrupt();
        try
        {
            // Give the shell a moment to process the interrupt and emit a fresh
            // prompt marker so the terminal can be reused by the next command.
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

    // prompt marker is injected via terminal environment during shell startup

    private static bool TryExtractOscMarker(string current, string markerPrefix, out int exitCode,
        out string cleanedOutput, int searchIndex = 0)
    {
        exitCode = 0;
        cleanedOutput = current;

        while (true)
        {
            var markerIndex = current.IndexOf(markerPrefix, searchIndex, StringComparison.Ordinal);
            if (markerIndex < 0) return false;

            var belIndex = current.IndexOf('\u0007', markerIndex);
            var stIndex = current.IndexOf("\u001b\\", markerIndex, StringComparison.Ordinal);

            var endIndex = belIndex;
            var endLength = 1;

            if (endIndex < 0 || (stIndex >= 0 && stIndex < endIndex))
            {
                endIndex = stIndex;
                endLength = 2;
            }

            if (endIndex < 0) return false;

            var markerContent = current.Substring(markerIndex + markerPrefix.Length,
                endIndex - (markerIndex + markerPrefix.Length));
            if (!int.TryParse(markerContent, out exitCode))
            {
                searchIndex = endIndex + endLength;
                continue;
            }

            cleanedOutput = current.Remove(markerIndex, endIndex - markerIndex + endLength);
            return true;
        }
    }

    private static string GetCommandEchoPrefix(string command)
    {
        var firstLine = command.TrimStart();
        var lineEnd = firstLine.IndexOfAny(['\r', '\n']);
        if (lineEnd >= 0)
            firstLine = firstLine[..lineEnd];

        const int minimumDistinctiveLength = 16;
        const int maximumPrefixLength = 48;
        if (firstLine.Length < minimumDistinctiveLength) return string.Empty;

        return firstLine[..Math.Min(firstLine.Length, maximumPrefixLength)];
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
