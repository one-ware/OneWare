using OneWare.Terminal.ViewModels;
using OneWare.TerminalManager.ViewModels;

namespace OneWare.TerminalManager.Models;

public class TerminalTabModel
{
    public string Title { get; }
    
    public TerminalViewModel Terminal { get; }
    
    public TerminalManagerViewModel Owner { get; }

    public TerminalTabModel(string title, TerminalViewModel terminal, TerminalManagerViewModel owner)
    {
        Title = title;
        Terminal = terminal;
        Owner = owner;
    }
    
    public void Close()
    {
        Owner.CloseTab(this);
        Terminal.Close();
    }
}