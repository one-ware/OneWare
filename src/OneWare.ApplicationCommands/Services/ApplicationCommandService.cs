using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OneWare.ApplicationCommands.Models;
using OneWare.ApplicationCommands.ViewModels;
using OneWare.ApplicationCommands.Views;
using OneWare.SDK.Controls;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using Prism.Ioc;

namespace OneWare.ApplicationCommands.Services;

public class ApplicationCommandService : IApplicationCommandService
{
    private readonly IWindowService _windowService;
    public ObservableCollection<IApplicationCommand> ApplicationCommands { get; } = new();

    private FlexibleWindow? _lastManagerWindow;
    
    public ApplicationCommandService(IWindowService windowService)
    {
        _windowService = windowService;
        
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>(HandleKeyDown);
        
        RegisterCommand(new LogicalApplicationCommand<TopLevel>("Open Actions", new KeyGesture(Key.Q, KeyModifiers.Control),
            x => OpenManager(x, "Actions")));
        
        RegisterCommand(new LogicalApplicationCommand<TopLevel>("Open Files", new KeyGesture(Key.T, KeyModifiers.Control),
            x => OpenManager(x, "Files")));
    }

    private void OpenManager(TopLevel topLevel, string startTab)
    {
        if (_lastManagerWindow?.IsAttachedToVisualTree() ?? false)
        {
            var manager = _lastManagerWindow.DataContext as CommandManagerViewModel;
            if (manager == null) throw new NullReferenceException(nameof(manager));
            manager.SelectedTab = manager.Tabs.First(t => t.Title == startTab);
        }
        else
        {
            var manager = ContainerLocator.Container.Resolve<CommandManagerViewModel>((typeof(ILogical), topLevel));
            manager.SelectedTab = manager.Tabs.First(t => t.Title == startTab);
            _lastManagerWindow = new CommandManagerView()
            {
                DataContext = manager
            };
            _windowService.Show(_lastManagerWindow, topLevel as Window);
        }
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
            if (command.Execute(logical))
            {
                args.Handled = true;
                return;
            };
        }
    }
}