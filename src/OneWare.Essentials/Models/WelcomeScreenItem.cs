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
    private IconModel? _iconModel;
    private IDisposable? _subscription;

    public WelcomeScreenItem(string id, string name, ICommand? command)
    {
        Id = id;
        Name = name;
        Command = command;
    }

    public string Id { get; set; }

    public IconModel? IconModel
    {
        get => _iconModel;
        set
        {
            _iconModel = value;
            if (value == null) Icon = null;
            _subscription?.Dispose();
            _subscription = value?.IconObservable?.Subscribe(x => Icon = x as IImage);
            if (value?.Icon != null) Icon = value.Icon;
        }
    }

    public string Name { get; }
    public ICommand? Command { get; }

    public IImage? Icon
    {
        get;
        set => SetProperty(ref field, value);
    }
}
