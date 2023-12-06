using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using OneWare.SDK.Models;
using OneWare.SDK.ViewModels;

namespace OneWare.ApplicationCommands.Models;

public class MenuItemApplicationCommand(MenuItemViewModel menuItem, string path) : IApplicationCommand
{
    public MenuItemViewModel MenuItem { get; } = menuItem;
    public string Name { get; } = $"{path}{menuItem.Header}";
    public KeyGesture? Gesture { get; } = menuItem.InputGesture;
    public IImage? Image { get; } = menuItem.ImageIcon;
    
    public bool Execute(ILogical source)
    {
        if (!CanExecute(source)) return false;
        MenuItem.Command?.Execute(MenuItem.CommandParameter);
        return true;
    }

    public bool CanExecute(ILogical source)
    {
        return MenuItem.Command?.CanExecute(MenuItem.CommandParameter) ?? false;
    }
}