using System.Collections.ObjectModel;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IApplicationCommandService
{
    public ObservableCollection<IApplicationCommand> ApplicationCommands { get; }
    public void RegisterCommand(IApplicationCommand command);
    public void LoadKeyConfiguration();
    public void SaveKeyConfiguration();
}