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
using OneWare.Terminal.ViewModels;
using OneWare.TerminalManager.Models;

namespace OneWare.TerminalManager.ViewModels;

public class TerminalManagerViewModel : ExtendedTool, ITerminalManagerService
{
    public const string IconKey = "Material.Console";
    private const string PromptMarkerPrefix = "\u001b]9;OW_DONE:";
    private readonly Dictionary<string, TerminalTabModel> _automationTerminals = new(StringComparer.Ordinal);
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
        RemoveAutomationMapping(tab);

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
        TimeSpan? timeout = null, bool closeWhenDone = true, CancellationToken cancellationToken = default)
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
        var resultTcs = new TaskCompletionSource<TerminalExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandSent = false;

        void OnDataReceived(object? sender, VtNetCore.Avalonia.DataReceivedEventArgs args)
        {
            var text = Encoding.UTF8.GetString(args.Data);
            output.Append(text);

            var current = output.ToString();
            while (TryExtractOscMarker(current, PromptMarkerPrefix, out var exitCode, out var cleaned))
            {
                current = cleaned;
                output.Clear();
                output.Append(cleaned);

                if (!commandSent) continue;

                resultTcs.TrySetResult(new TerminalExecutionResult(cleaned, exitCode, false));
                break;
            }
        }

        terminal.Connection.DataReceived += OnDataReceived;
        commandSent = true;
        terminal.Send(command);

        TerminalExecutionResult result;

        try
        {
            result = await WaitForResultAsync(resultTcs.Task, timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            result = new TerminalExecutionResult(output.ToString(), -1, true);
        }
        finally
        {
            terminal.Connection.DataReceived -= OnDataReceived;
            if (closeWhenDone) terminal.Close();
        }

        return result;
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
        out string cleanedOutput)
    {
        exitCode = 0;
        cleanedOutput = current;

        var markerIndex = current.IndexOf(markerPrefix, StringComparison.Ordinal);
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
        int.TryParse(markerContent, out exitCode);

        cleanedOutput = current.Remove(markerIndex, endIndex - markerIndex + endLength);
        return true;
    }

    public Task<TerminalExecutionResult> ExecuteInTerminalAsync(string command, string id,
        string? workingDirectory = null, bool showInUi = false, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        _mainDockService.Show<ITerminalManagerService>();
        
        var tab = GetOrCreateAutomationTab(id, workingDirectory, showInUi, id);
        return ExecuteInTerminalAsync(tab.Terminal, command, timeout, closeWhenDone: false, cancellationToken);
    }

    private TerminalTabModel GetOrCreateAutomationTab(string terminalId, string? workingDirectory, bool select,
        string? name)
    {
        if (_automationTerminals.TryGetValue(terminalId, out var existing))
        {
            if (select) SelectedTerminalTab = existing;
            return existing;
        }

        var tab = NewTerminal(string.IsNullOrWhiteSpace(name) ? terminalId : name, workingDirectory, select);
        _automationTerminals[terminalId] = tab;
        return tab;
    }

    private void RemoveAutomationMapping(TerminalTabModel tab)
    {
        var mapping = _automationTerminals.FirstOrDefault(entry => ReferenceEquals(entry.Value, tab));
        if (!string.IsNullOrEmpty(mapping.Key))
        {
            _automationTerminals.Remove(mapping.Key);
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
