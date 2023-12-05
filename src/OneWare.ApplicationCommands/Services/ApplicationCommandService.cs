using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using OneWare.ApplicationCommands.Models;
using OneWare.ApplicationCommands.ViewModels;
using OneWare.ApplicationCommands.Views;
using OneWare.SDK.Services;
using Prism.Ioc;

namespace OneWare.ApplicationCommands.Services;

public class ApplicationCommandService : IApplicationCommandService
{
    public ObservableCollection<IApplicationCommand> ApplicationCommands { get; } = new();

    public ApplicationCommandService(IWindowService windowService)
    {
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>(HandleKeyDown, handledEventsToo: false);
        
        RegisterCommand(new LogicalApplicationCommand<TopLevel>("Open Command Manager", new KeyGesture(Key.Q, KeyModifiers.Control),
            x =>
            {
                var window = new CommandManagerView()
                {
                    DataContext = ContainerLocator.Container.Resolve<CommandManagerViewModel>()
                };
                windowService.Show(window, x as Window);
                window.Focus();
            }));
    }
    
    public void RegisterCommand(IApplicationCommand command)
    {
        ApplicationCommands.Add(command);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs args)
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