using Avalonia.Media;
using ImTools;
using OneWare.Shared;
using OneWare.Terminal.ViewModels;

namespace OneWare.TerminalManager.ViewModels;

public class StandaloneTerminalViewModel : ExtendedTool
{
    public TerminalViewModel Terminal { get; }
    public StandaloneTerminalViewModel(string title, TerminalViewModel terminal) : base("BoxIcons.RegularTerminal")
    {
        Title = title;
        Id = title;
        Terminal = terminal;
    }
}