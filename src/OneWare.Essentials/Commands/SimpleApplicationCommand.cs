using Avalonia.LogicalTree;

namespace OneWare.Essentials.Commands;

public class SimpleApplicationCommand(string name, Action action, Func<bool>? canExecute = null)
    : ApplicationCommandBase(name)
{
    public override bool Execute(ILogical source)
    {
        action.Invoke();
        return true;
    }

    public override bool CanExecute(ILogical source)
    {
        return canExecute?.Invoke() ?? true;
    }
}