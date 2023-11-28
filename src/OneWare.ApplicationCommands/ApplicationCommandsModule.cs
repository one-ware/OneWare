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

        InputElement.KeyDownEvent.AddClassHandler<TopLevel>((sender, args) =>
        {
            commandService.HandleKeyDown(sender, args);
        }, handledEventsToo: false);

        commandService.RegisterCommand(
            new LogicalDataContextApplicationCommand<IExtendedDocument>("Save File",
                new KeyGesture(Key.S, PlatformHelper.ControlKey),
                x =>
                {
                    _ = x.SaveAsync();
                }));
    }
}