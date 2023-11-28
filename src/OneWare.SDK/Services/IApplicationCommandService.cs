using OneWare.ApplicationCommands.Models;

namespace OneWare.SDK.Services;

public interface IApplicationCommandService
{
    public void RegisterCommand(IApplicationCommand command);
}