using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Models;
using OneWare.Terminal.ViewModels;

namespace OneWare.TerminalManager.ViewModels;

/// <summary>
/// A terminal rendered inside another view (currently the AI chat tool box) instead of the
/// terminal pane, plus the header and the "open in terminal pane" action around it.
/// </summary>
public class EmbeddedTerminalViewModel : ObservableObject, IEmbeddedTerminal
{
    private readonly TerminalManagerViewModel _owner;

    public EmbeddedTerminalViewModel(TerminalManagerViewModel owner, string title, string command,
        TerminalViewModel terminal)
    {
        _owner = owner;
        Title = title;
        Command = command;
        Terminal = terminal;
        ShowInPaneCommand = new RelayCommand(() => _owner.ShowEmbeddedTerminalInPane(this));

        // A shell that exited on its own must not be restarted when the host view is rebuilt
        // (e.g. after the chat message was scrolled out of the virtualized list).
        Terminal.ConnectionClosed += (_, _) => MarkShellClosed();
    }

    public string Title { get; }

    public string Command { get; }

    public TerminalViewModel Terminal { get; }

    public IRelayCommand ShowInPaneCommand { get; }

    /// <summary>
    /// The terminal while this view is responsible for rendering it, otherwise null. Two
    /// terminal controls must never be bound to the same terminal: each of them consumes the
    /// shell's byte stream and resizes the pty, so both the output and the layout would break.
    /// </summary>
    public TerminalViewModel? HostedTerminal => IsShownInPane ? null : Terminal;

    public bool IsShownInPane
    {
        get;
        internal set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(HostedTerminal));
        }
    }

    public bool IsRunning
    {
        get;
        internal set => SetProperty(ref field, value);
    }

    public bool IsShellClosed
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public void CloseShell()
    {
        if (IsShownInPane || IsShellClosed) return;
        MarkShellClosed();
        Terminal.Close();
    }

    private void MarkShellClosed()
    {
        if (IsShownInPane) return;
        // The rendered output stays on screen, only the shell process is released.
        Terminal.AllowConnect = false;
        IsShellClosed = true;
    }
}
