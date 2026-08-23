using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Models;
using OneWare.Essentials.Debugger.Interfaces;

namespace OneWare.Debugger.ViewModels.Main;

// Reiter "Debugger Console": der Verkehr mit dem Backend und die Zeile, ueber die sich
// Kommandos direkt absetzen lassen.
// Herein kommen die Zeilen von DebuggerViewModel, das als Einziges an der Sitzung haengt;
// hinaus gehen die Kommandos direkt an den Dienst.
public partial class ConsoleTabViewModel : ObservableObject
{
    private readonly IDebuggerService _debuggerService;

    [ObservableProperty] private string _commandText = string.Empty;

    // Gespiegelt statt abgefragt -> das Kommandofeld muss sich sperren koennen, ohne selbst am
    // Zustand des Debuggers zu haengen.
    [ObservableProperty] private bool _isSessionActive;

    public ConsoleTabViewModel(IDebuggerService debuggerService)
    {
        _debuggerService = debuggerService;
    }

    // Verkehr mit dem Debugger-Backend, inklusive abgesetzter Kommandos.
    public ObservableCollection<ConsoleLine> Lines { get; } = [];

    // Die Session meldet sich aus ihrem Lesethread; die Sammlung haengt aber an der Ansicht
    // und darf nur vom UI-Thread angefasst werden.
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
