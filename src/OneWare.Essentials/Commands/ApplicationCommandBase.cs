using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Commands;

public abstract class ApplicationCommandBase(string name) : ObservableObject, IApplicationCommand
{
    private KeyGesture? _activeGesture;

    public string Name { get; } = name;
    
    public string? Detail { get; set; }

    public KeyGesture? ActiveGesture
    {
        get => _activeGesture;
        set => SetProperty(ref _activeGesture, value);
    }

    public KeyGesture? DefaultGesture
    {
        get;
        init
        {
            field = value;
            _activeGesture = value;
        }
    }

    public IconModel? Icon
    {
        get;
        set => SetProperty(ref field, value);
    }

    public abstract bool Execute(ILogical source);

    public abstract bool CanExecute(ILogical source);
}
