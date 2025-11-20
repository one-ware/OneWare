using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OneWare.Essentials.Services;

namespace OneWare.Core.Models;

public sealed class WelcomeScreenStartItem(string id, string name, string? icon, ICommand? command) 
    : WelcomeScreenItem(id, name, icon, command), IWelcomeScreenStartItem
{
}

public sealed class WelcomeScreenWalkthroughItem(string id, string name, string? description, string? icon, ICommand? command) 
    : WelcomeScreenItem(id, name, icon, command), IWelcomeScreenWalkthroughItem
{
    public bool HideDescription { get; } = description == null;
    public string? Description { get; } = description;
}

public abstract class WelcomeScreenItem
    : IWelcomeScreenItem
{
    public WelcomeScreenItem(string id, string name, string? icon, ICommand? command)
    {
        Id = id;
        Name = name;
        Command = command;

        if (icon == null) return;
        
        //the icon could be IImage or StreamGeometry
        Icon = Application.Current?.FindResource(icon);
        IconIsGeometry = Icon is Geometry;
    }
    
    public string Id { get; set; }
    public string Name { get; }
    public object? Icon { get; }
    public bool IconIsGeometry { get; }
    public ICommand? Command { get; }
    
    public event PropertyChangedEventHandler? PropertyChanged;
}