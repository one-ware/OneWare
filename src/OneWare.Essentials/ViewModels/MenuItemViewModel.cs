using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.ViewModels;

public class MenuItemViewModel(string partId)
    : ObservableObject, ICanHaveObservableItems<MenuItemViewModel>, ICanHaveIcon
{
    private ICommand? _command;

    private object? _commandParameter;

    private string? _header;

    private IImage? _icon;

    private KeyGesture? _inputGesture;

    private bool _isEnabled = true;

    private ObservableCollection<MenuItemViewModel>? _items;

    private IObservable<object?>? _iconObservable;
    
    private IDisposable? _subscription;
    public string PartId { get; } = partId;
    public int Priority { get; init; }

    public ICommand? Command
    {
        get => _command;
        set => SetProperty(ref _command, value);
    }

    public object? CommandParameter
    {
        get => _commandParameter;
        set => SetProperty(ref _commandParameter, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string? Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
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

    public KeyGesture? InputGesture
    {
        get => _inputGesture;
        set => SetProperty(ref _inputGesture, value);
    }

    public IImage? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public ObservableCollection<MenuItemViewModel>? Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    public string Name => Header ?? string.Empty;
}