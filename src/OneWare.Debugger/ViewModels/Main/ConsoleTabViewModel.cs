using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Models;
using OneWare.Essentials.Debugger.Interfaces;

namespace OneWare.Debugger.ViewModels.Main;

// "Debugger Console" tab: the traffic with the backend and the line through which
// commands can be issued directly.
// The lines come in from DebuggerViewModel, the only one attached to the session;
// the commands go out directly to the service.
public partial class ConsoleTabViewModel : ObservableObject
{
    private readonly IDebuggerService _debuggerService;

    [ObservableProperty] private string _commandText = string.Empty;

    // Mirrored instead of queried -> the command field must be able to lock without itself
    // depending on the debugger's state.
    [ObservableProperty] private bool _isSessionActive;

    public ConsoleTabViewModel(IDebuggerService debuggerService)
    {
        _debuggerService = debuggerService;
    }

    // Traffic with the debugger backend, including issued commands.
    public ObservableCollection<ConsoleLine> Lines { get; } = [];

    // The session reports from its reader thread; the collection, however, is bound to the view
    // and may only be touched from the UI thread.
    public void Append(string line, bool isCommand = false)
    {
        Dispatcher.UIThread.Post(() => Lines.Add(new ConsoleLine(line, isCommand)));
    }

    public void Clear()
    {
        Lines.Clear();
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(CommandText)) return Task.CompletedTask;

        var command = CommandText;
        CommandText = string.Empty;
        return _debuggerService.SendRawCommandAsync(command);
    }

    partial void OnIsSessionActiveChanged(bool value)
    {
        SendCommandCommand.NotifyCanExecuteChanged();
    }
}
