using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Commands;

public abstract class ApplicationCommandBase(string name) : ObservableObject, IApplicationCommand
{
    private readonly KeyGesture? _defaultGesture;

    private KeyGesture? _activeGesture;

    private IImage? _icon;

    private IDisposable? _subscription;

    public IObservable<object?>? IconObservable
    {
        set
        {
            _subscription?.Dispose();
            _subscription = value?.Subscribe(x => Icon = x as IImage);
        }
    }

    public string Name { get; } = name;

    public KeyGesture? ActiveGesture
    {
        get => _activeGesture;
        set => SetProperty(ref _activeGesture, value);
    }

    public KeyGesture? DefaultGesture
    {
        get => _defaultGesture;
        init
        {
            _defaultGesture = value;
            _activeGesture = value;
        }
    }

    public IImage? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public abstract bool Execute(ILogical source);

    public abstract bool CanExecute(ILogical source);
}