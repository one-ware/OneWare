using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

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

public interface IWelcomeScreenReceiver
{
    void HandleRegisterItemToNew(IWelcomeScreenStartItem item);
    void HandleRegisterItemToOpen(IWelcomeScreenStartItem item);
    void HandleRegisterItemToWalkthrough(IWelcomeScreenWalkthroughItem item);
}

public class WelcomeScreenService : IWelcomeScreenService
{
    private readonly IDictionary<string, IWelcomeScreenStartItem> _startItems = 
            new  Dictionary<string, IWelcomeScreenStartItem>();
    private readonly IDictionary<string, IWelcomeScreenWalkthroughItem> _walkthroughItems = 
            new  Dictionary<string, IWelcomeScreenWalkthroughItem>();
    
    private IWelcomeScreenReceiver? _receiver;

    internal void RegisterReceiver(IWelcomeScreenReceiver receiver)
    {
        _receiver = receiver;
    }
    
    public void RegisterItemToNew(string id, IWelcomeScreenStartItem item)
    {
        if (_receiver == null)
            throw new InvalidOperationException();
        
        _startItems.Add(id, item);
        _receiver.HandleRegisterItemToNew(item);
    }
    public void RegisterItemToOpen(string id, IWelcomeScreenStartItem item)
    {
        if (_receiver == null)
            throw new InvalidOperationException();
        
        _startItems.Add(id, item);
        _receiver.HandleRegisterItemToOpen(item);
    }
    public void RegisterItemToWalkthrough(string id, IWelcomeScreenWalkthroughItem item)
    {
        if (_receiver == null)
            throw new InvalidOperationException();
        
        _walkthroughItems.Add(id, item);
        _receiver.HandleRegisterItemToWalkthrough(item);
    }
    public bool StartItemIsRegistered(string id)
    {
        return _startItems.ContainsKey(id);
    }
    public bool WalkthroughItemIsRegistered(string id)
    {
        return _walkthroughItems.ContainsKey(id);
    }
}