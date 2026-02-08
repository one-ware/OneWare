using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.ViewModels;

public class MenuItemViewModel(string partId)
    : ObservableObject, ICanHaveObservableItems<MenuItemViewModel>
{
    public string PartId { get; } = partId;
    public int Priority { get; init; }

    public ICommand? Command
    {
        get;
        set => SetProperty(ref field, value);
    }

    public object? CommandParameter
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsEnabled
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public string? Header
    {
        get;
        set => SetProperty(ref field, value);
    }

    public IconModel? IconModel
    {
        get;
        set => SetProperty(ref field, value);
    }

    public KeyGesture? InputGesture
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ObservableCollection<MenuItemViewModel>? Items
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string Name => Header ?? string.Empty;
}