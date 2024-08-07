using OneWare.Essentials.ViewModels;
using OneWare.Terminal.ViewModels;

namespace OneWare.TerminalManager.ViewModels;

public class StandaloneTerminalViewModel : ExtendedTool
{
    public StandaloneTerminalViewModel(string title, TerminalViewModel terminal) : base("BoxIcons.RegularTerminal")
    {
        Title = title;
        Id = title;
        Terminal = terminal;
    }

    public TerminalViewModel Terminal { get; }
}