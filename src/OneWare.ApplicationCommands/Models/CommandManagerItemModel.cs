using OneWare.Essentials.Models;

namespace OneWare.ApplicationCommands.Models;

public class CommandManagerItemModel(IApplicationCommand command, bool isEnabled)
{
    public IApplicationCommand Command { get; } = command;

    public bool IsEnabled { get; } = isEnabled;
}