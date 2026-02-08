using Avalonia.LogicalTree;
using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Commands;

public class MenuItemApplicationCommand : ApplicationCommandBase
{
    public MenuItemApplicationCommand(MenuItemModel menuItem, string path) : base($"{path}{menuItem.Header}")
    {
        MenuItem = menuItem;
        Icon = menuItem.Icon;
        menuItem.WhenValueChanged(x => x.Icon).Subscribe(x => Icon = x);

        DefaultGesture = menuItem.InputGesture;

        this.WhenValueChanged(x => x.ActiveGesture).Subscribe(x => { MenuItem.InputGesture = x; });
    }

    public MenuItemModel MenuItem { get; }

    public override bool Execute(ILogical source)
    {
        if (!CanExecute(source)) return false;
        MenuItem.Command?.Execute(MenuItem.CommandParameter);
        return true;
    }

    public override bool CanExecute(ILogical source)
    {
        return MenuItem.Command?.CanExecute(MenuItem.CommandParameter) ?? false;
    }
}
