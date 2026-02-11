using System.Collections.ObjectModel;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IApplicationCommandService
{
    /// <summary>
    /// Registered application commands.
    /// </summary>
    public ObservableCollection<IApplicationCommand> ApplicationCommands { get; }
    /// <summary>
    /// Registers a command and makes it available for key bindings.
    /// </summary>
    public void RegisterCommand(IApplicationCommand command);
    /// <summary>
    /// Loads persisted key bindings.
    /// </summary>
    public void LoadKeyConfiguration();
    /// <summary>
    /// Saves key bindings.
    /// </summary>
    public void SaveKeyConfiguration();
}
