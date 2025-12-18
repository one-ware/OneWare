using System.ComponentModel;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
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
    private IObservable<object?>? _iconObservable;
    private IDisposable? _subscription;
    
    public WelcomeScreenItem(string id, string name, ICommand? command)
    {
        Id = id;
        Name = name;
        Command = command;
    }
    
    public string Id { get; set; }
    public string Name { get; }
    public ICommand? Command { get; }
    
    public IImage? Icon
    {
        get;
        set => SetProperty(ref field, value);
    }
    
    public IObservable<object?>? IconObservable
    {
        get => _iconObservable;
        set
        {
            _iconObservable = value;
            if (value == null)
            {
                Icon = null;
            }
            _subscription?.Dispose();
            _subscription = value?.Subscribe(x => Icon = x as IImage);
        }
    }
}