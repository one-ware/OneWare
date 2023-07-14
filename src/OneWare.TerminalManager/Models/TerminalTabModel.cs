using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Terminal.ViewModels;
using OneWare.TerminalManager.ViewModels;

namespace OneWare.TerminalManager.Models;

public class TerminalTabModel : ObservableObject
{
    private string _title;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    
    public TerminalViewModel Terminal { get; }
    
    public TerminalManagerViewModel Owner { get; }

    public TerminalTabModel(string title, TerminalViewModel terminal, TerminalManagerViewModel owner)
    {
        _title = title;
        Terminal = terminal;
        Owner = owner;
    }
    
    public void Close()
    {
        Owner.CloseTab(this);
        Terminal.Close();
    }
}