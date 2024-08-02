using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using OneWare.ApplicationCommands.Serialization;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ApplicationCommands.Services;

public class ApplicationCommandService : IApplicationCommandService
{
    private readonly string _keyConfigFile;

    public ApplicationCommandService(IPaths paths)
    {
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>(HandleKeyDown);

        _keyConfigFile = Path.Combine(paths.AppDataDirectory, "keyConfig.json");
    }

    public ObservableCollection<IApplicationCommand> ApplicationCommands { get; } = new();

    public void RegisterCommand(IApplicationCommand command)
    {
        ApplicationCommands.Add(command);
    }

    public void LoadKeyConfiguration()
    {
        KeyConfigSerializer.LoadHotkeys(_keyConfigFile, ApplicationCommands);
    }

    public void SaveKeyConfiguration()
    {
        KeyConfigSerializer.SaveHotkeys(_keyConfigFile, ApplicationCommands);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs args)
    {
        var gesture = new KeyGesture(args.Key, args.KeyModifiers);

        if (args.Source is not ILogical logical) return;

        var commands = ApplicationCommands.Where(x => x.ActiveGesture?.Equals(gesture) ?? false);

        foreach (var command in commands)
        {
            if (command.Execute(logical))
            {
                args.Handled = true;
                return;
            }
        }
    }
}