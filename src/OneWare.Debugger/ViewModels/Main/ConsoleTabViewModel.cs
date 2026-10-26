using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.Models;
using OneWare.Essentials.Debugger.Interfaces;

namespace OneWare.Debugger.ViewModels.Main;

// "Console" tab: the traffic with the backend and the line through which commands can be issued directly.
// The lines come in from DebuggerViewModel, the only one attached to the session;
// the commands go out directly to the service.
public partial class ConsoleTabViewModel : ObservableObject
{
    // Enough for a long session; beyond that the oldest lines go.
    public const int MaxLines = 5000;

    private readonly IDebuggerService _debuggerService;
    private readonly List<ConsoleLine> _allLines = [];
    private readonly List<string> _history = [];

    // Commands the user sent that have not come back through CommandSent yet -> tells them apart
    // from the studio's own commands.
    private readonly Queue<string> _pendingUserCommands = new();

    // GDB echoes CLI commands on its log stream; the first line equal to the command is that echo.
    private string? _pendingEcho;
    private ConsoleLineKind _echoKind;

    private bool _afterUserCommand;
    private int _historyIndex;

    [ObservableProperty] private string _commandText = string.Empty;

    // Mirrored instead of queried -> the command field must be able to lock without itself
    // depending on the debugger's state.
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SendCommandCommand))]
    private bool _isSessionActive;

    [ObservableProperty] private bool _showProtocol;

    public ConsoleTabViewModel(IDebuggerService debuggerService)
    {
        _debuggerService = debuggerService;
    }

    // The lines currently shown (protocol lines only with ShowProtocol).
    public ObservableCollection<ConsoleLine> Lines { get; } = [];

    // The Append* methods may be called from any thread (the session reports from its reader thread);
    // the Receive* methods do the work and must run on the UI thread.
    public void AppendOutput(string text) => Dispatcher.UIThread.Post(() => ReceiveOutput(text));

    public void AppendCommand(string command) => Dispatcher.UIThread.Post(() => ReceiveCommand(command));

    public void AppendInfo(string text) => Dispatcher.UIThread.Post(() => ReceiveInfo(text));

    // Program and GDB output.
    public void ReceiveOutput(string text)
    {
        if (_pendingEcho != null && text.Trim() == _pendingEcho)
        {
            _pendingEcho = null;
            if (_echoKind != ConsoleLineKind.Command) Add(new ConsoleLine(text, _echoKind));
            return;
        }

        var kind = ConsoleLineClassifier.Classify(text, _afterUserCommand);

        // A failing CLI command reports the message twice: on the log stream and in ^error.
        if (kind == ConsoleLineKind.Error && _allLines.Count > 0 && _allLines[^1].Kind == ConsoleLineKind.Output &&
            text.StartsWith(ConsoleLineClassifier.ErrorPrefix, StringComparison.Ordinal) &&
            _allLines[^1].Text == text[ConsoleLineClassifier.ErrorPrefix.Length..])
            RemoveLast();

        Add(new ConsoleLine(text, kind));
    }

    // A command that went to the backend, from the user or from the studio.
    public void ReceiveCommand(string command)
    {
        var text = command.Trim();
        _afterUserCommand = _pendingUserCommands.TryPeek(out var user) && user == text;
        if (_afterUserCommand) _pendingUserCommands.Dequeue();

        var kind = _afterUserCommand ? ConsoleLineKind.Command : ConsoleLineKind.Protocol;
        _pendingEcho = text;
        _echoKind = kind;

        Add(new ConsoleLine(text, kind));
    }

    // A note from the studio, e.g. why a start failed.
    public void ReceiveInfo(string text)
    {
        Add(new ConsoleLine(text, ConsoleLineKind.Info));
    }

    [RelayCommand]
    public void Clear()
    {
        _allLines.Clear();
        Lines.Clear();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private Task SendCommandAsync()
    {
        var command = CommandText.Trim();
        if (command.Length == 0) return Task.CompletedTask;

        if (_history.Count == 0 || _history[^1] != command) _history.Add(command);
        _historyIndex = _history.Count;

        _pendingUserCommands.Enqueue(command);
        CommandText = string.Empty;
        return _debuggerService.SendRawCommandAsync(command);
    }

    // Up/Down in the command field, like a shell.
    public void BrowseHistory(int direction)
    {
        if (_history.Count == 0) return;

        _historyIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count);
        CommandText = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
    }

    public string GetText()
    {
        return string.Join(Environment.NewLine, Lines.Select(x => x.Text));
    }

    partial void OnShowProtocolChanged(bool value)
    {
        Lines.Clear();
        foreach (var line in _allLines.Where(IsVisible)) Lines.Add(line);
    }

    private void Add(ConsoleLine line)
    {
        _allLines.Add(line);
        if (IsVisible(line)) Lines.Add(line);

        if (_allLines.Count <= MaxLines) return;

        var oldest = _allLines[0];
        _allLines.RemoveAt(0);
        if (Lines.Count > 0 && ReferenceEquals(Lines[0], oldest)) Lines.RemoveAt(0);
    }

    private void RemoveLast()
    {
        var last = _allLines[^1];
        _allLines.RemoveAt(_allLines.Count - 1);
        if (Lines.Count > 0 && ReferenceEquals(Lines[^1], last)) Lines.RemoveAt(Lines.Count - 1);
    }

    private bool IsVisible(ConsoleLine line) => ShowProtocol || line.Kind != ConsoleLineKind.Protocol;
}
