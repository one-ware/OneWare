using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using AvaloniaEdit;
using CommunityToolkit.Mvvm.Input;
using OneWare.ApplicationCommands.Models;
using OneWare.ApplicationCommands.Services;
using OneWare.SDK.Extensions;
using OneWare.SDK.Helpers;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.ApplicationCommands;

public class ApplicationCommandsModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IApplicationCommandService, ApplicationCommandService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var commandService = containerProvider.Resolve<ApplicationCommandService>();
        var windowService = containerProvider.Resolve<IWindowService>();

        InputElement.KeyDownEvent.AddClassHandler<TopLevel>((sender, args) =>
        {
            commandService.HandleKeyDown(sender, args);
        }, handledEventsToo: false);
        
        var saveFileCommand = new AsyncRelayCommand(() => containerProvider.Resolve<IDockService>().CurrentDocument?.SaveAsync() ?? Task.FromResult(false));

        var inputGesture = new KeyGesture(Key.S, PlatformHelper.ControlKey);
            
        windowService.RegisterMenuItem("MainWindow_MainMenu/File", new MenuItemViewModel("Save")
        {
            Command = saveFileCommand,
            Header = "Save File",
            InputGesture = inputGesture,
        });

        commandService.RegisterCommand(
            new LogicalDataContextApplicationCommand<IExtendedDocument>("Save File", inputGesture,
                x =>
                {
                    _ = x.SaveAsync();
                })
        );
    }
}