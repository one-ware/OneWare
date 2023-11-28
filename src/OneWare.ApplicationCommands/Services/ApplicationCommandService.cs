using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.LogicalTree;
using OneWare.ApplicationCommands.Models;
using OneWare.SDK.Services;

namespace OneWare.ApplicationCommands.Services;

public class ApplicationCommandService : IApplicationCommandService
{
    public ObservableCollection<IApplicationCommand> ApplicationCommands { get; } = new();
    
    public void RegisterCommand(IApplicationCommand command)
    {
        ApplicationCommands.Add(command);
    }

    public void HandleKeyDown(object? sender, KeyEventArgs args)
    {
        var gesture = new KeyGesture(args.Key, args.KeyModifiers);

        if (args.Source is not ILogical logical) return;
        
        var commands = ApplicationCommands.Where(x => x.Gesture?.Equals(gesture) ?? false);
        
        foreach (var command in commands)
        {
            command.Execute(logical);
        }
    }
}