using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Commands;

public static class DocumentCommands
{
    public static IAsyncRelayCommand<IExtendedDocument?> CopyPath { get; } =
        new AsyncRelayCommand<IExtendedDocument?>(CopyPathAsync);

    private static async Task CopyPathAsync(IExtendedDocument? document)
    {
        if (document == null) return;

        var dockService = ContainerLocator.Container.Resolve<IMainDockService>();
        var clipboard = dockService.GetWindowOwner(document)?.Clipboard;
        if (clipboard == null) return;

        await clipboard.SetTextAsync(document.FullPath);
    }
}
