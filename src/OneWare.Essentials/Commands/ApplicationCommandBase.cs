using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Commands;

public abstract class ApplicationCommandBase(string name) : ObservableObject, IApplicationCommand
{
    public string Name { get; } = name;

    private KeyGesture? _activeGesture;
    public KeyGesture? ActiveGesture
    {
        get => _activeGesture; 
        set => SetProperty(ref _activeGesture, value);
    }

    private readonly KeyGesture? _defaultGesture;
    public KeyGesture? DefaultGesture
    {
        get => _defaultGesture;
        init
        {
            _defaultGesture = value;
            _activeGesture = value;
        } 
    }
    
    private IImage? _icon;
    public IImage? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    private IDisposable? _subscription;
    public IObservable<object?>? IconObservable
    {
        set
        {
            _subscription?.Dispose();
            _subscription = value?.Subscribe(x => Icon = x as IImage);
        }
    }

    public abstract bool Execute(ILogical source);

    public abstract bool CanExecute(ILogical source);
}