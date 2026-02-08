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
    private IconModel? _iconModel;

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

    public IconModel? IconModel
    {
        get => _iconModel;
        set
        {
            if (!SetProperty(ref _iconModel, value)) return;
            _subscription?.Dispose();
            _subscription = value?.IconObservable?.Subscribe(x => Icon = x as IImage);
            if (value?.Icon != null) Icon = value.Icon;
        }
    }

    public abstract bool Execute(ILogical source);

    public abstract bool CanExecute(ILogical source);
}
