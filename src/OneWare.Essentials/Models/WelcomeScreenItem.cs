using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.Models;

public interface IWelcomeScreenStartItem : IWelcomeScreenItem
{
}

public interface IWelcomeScreenWalkthroughItem : IWelcomeScreenItem
{
}

public sealed class WelcomeScreenStartItem(string id, string name, ICommand? command)
    : WelcomeScreenItem(id, name, command), IWelcomeScreenStartItem
{
}

public sealed class WelcomeScreenWalkthroughItem(string id, string name, string? description, ICommand? command)
    : WelcomeScreenItem(id, name, command), IWelcomeScreenWalkthroughItem
{
    public bool HideDescription { get; } = description == null;
    public string? Description { get; } = description;
}

public abstract class WelcomeScreenItem : ObservableObject, IWelcomeScreenItem
{
    public WelcomeScreenItem(string id, string name, ICommand? command)
    {
        Id = id;
        Name = name;
        Command = command;
    }

    public string Id { get; set; }

    public IconModel? Icon
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string Name { get; }
    public ICommand? Command { get; }
}
