using System.Windows.Input;
using Avalonia.LogicalTree;

namespace OneWare.Essentials.Commands;

public class CommandApplicationCommand : ApplicationCommandBase
{
    private readonly ICommand _command;

    public CommandApplicationCommand(string name, ICommand command) : base(name)
    {
        _command = command;
    }

    public override bool Execute(ILogical source)
    {
        if (!CanExecute(source)) return false;
        _command.Execute(source);
        return true;
    }

    public override bool CanExecute(ILogical source)
    {
        return _command.CanExecute(source);
    }
}