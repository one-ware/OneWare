using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Models;

public class MenuItemModel(string partId)
    : ObservableObject, ICanHaveObservableItems<MenuItemModel>
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

    public IconModel? Icon
    {
        get;
        set => SetProperty(ref field, value);
    }

    public KeyGesture? InputGesture
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ObservableCollection<MenuItemModel>? Items
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string Name => Header ?? string.Empty;
}