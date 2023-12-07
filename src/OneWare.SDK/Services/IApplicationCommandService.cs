using System.Collections.ObjectModel;
using OneWare.SDK.Models;

namespace OneWare.SDK.Services;

public interface IApplicationCommandService
{
    public ObservableCollection<IApplicationCommand> ApplicationCommands { get; }
    public void RegisterCommand(IApplicationCommand command);
    public void LoadKeyConfiguration();
    public void SaveKeyConfiguration();
}