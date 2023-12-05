using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.ApplicationCommands.Models;

public class CommandManagerTabModel(string title) : ObservableObject
{
    public string Title { get; } = title;
}