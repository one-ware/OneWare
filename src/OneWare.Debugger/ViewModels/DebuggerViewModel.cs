using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

/// <summary>
///     Unteres Panel: Steuerleiste der Debug-Session sowie die Reiter Console, Registers
///     und Debugger Console.
/// </summary>
public partial class DebuggerViewModel : ExtendedTool
{
    public const string IconKey = "VsCodeLight.Debug";

    /// <summary>
    ///     Eingabezeile der Debugger Console, ueber die sich Kommandos direkt an das Backend
    ///     schicken lassen.
    /// </summary>
    [ObservableProperty] private string _commandText = string.Empty;

    [ObservableProperty] private bool _isRunning;

    /// <summary>
    ///     Solange keine Session laeuft, sind alle Steuerbefehle deaktiviert. Die Anbindung an
    ///     eine echte Session erfolgt ueber den Debug-Adapter und setzt dieses Flag.
    /// </summary>
    [ObservableProperty] private bool _isSessionActive;

    [ObservableProperty] private string _statusText = "No session";

    public DebuggerViewModel() : base(IconKey)
    {
        Id = "Debug";
        Title = "Debug";
    }

    /// <summary>Ausgaben des debuggten Programms.</summary>
    public ObservableCollection<string> Console { get; } = [];

    /// <summary>Registerinhalte der laufenden Session.</summary>
    public ObservableCollection<RegisterRow> Registers { get; } = [];

    /// <summary>Roher Verkehr mit dem Debugger-Backend, inklusive abgesetzter Kommandos.</summary>
    public ObservableCollection<string> DebuggerConsole { get; } = [];

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void Pause()
    {
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void Continue()
    {
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void Stop()
    {
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void StepOver()
    {
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void StepInto()
    {
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void StepOut()
    {
    }

    [RelayCommand(CanExecute = nameof(IsSessionActive))]
    private void SendCommand()
    {
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
