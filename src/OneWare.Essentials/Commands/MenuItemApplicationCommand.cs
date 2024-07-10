using Avalonia.LogicalTree;
using DynamicData.Binding;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Commands;

public class MenuItemApplicationCommand : ApplicationCommandBase
{
    public MenuItemViewModel MenuItem { get; }

    public MenuItemApplicationCommand(MenuItemViewModel menuItem, string path) : base(($"{path}{menuItem.Header}"))
    {
        MenuItem = menuItem;
        IconObservable = menuItem.WhenValueChanged(x => x.Icon);

        DefaultGesture = menuItem.InputGesture;
        
        this.WhenValueChanged(x => x.ActiveGesture).Subscribe(x =>
        {
            MenuItem.InputGesture = x;
        });
    }
    
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