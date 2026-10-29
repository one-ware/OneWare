using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OneWare.Chat.ViewModels.ChatMessages;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Chat.Views.ChatMessages;

public partial class ChatMessageImageView : UserControl
{
    public ChatMessageImageView()
    {
        InitializeComponent();
    }

    private ChatMessageImageViewModel? Message => DataContext as ChatMessageImageViewModel;

    private void OnImageTapped(object? sender, TappedEventArgs e) => Message?.Open();

    private void OnOpenClick(object? sender, RoutedEventArgs e) => Message?.Open();

    private void OnShowInFolderClick(object? sender, RoutedEventArgs e)
    {
        if (Message is { } message && File.Exists(message.FilePath))
            PlatformHelper.OpenExplorerPath(message.FilePath);
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        if (Message is not { } message || !File.Exists(message.FilePath)) return;
        if (TopLevel.GetTopLevel(this) is not { } topLevel) return;

        var extension = Path.GetExtension(message.FilePath);
        try
        {
            var target = await StorageProviderHelper.SelectSaveFileAsync(topLevel, "Save Image", null,
                extension.TrimStart('.'), $"image{extension}", true,
                new FilePickerFileType("Image") { Patterns = [$"*{extension}"], MimeTypes = [message.MimeType] });
            if (target == null) return;

            File.Copy(message.FilePath, target, true);
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                ?.Error($"Saving the image failed: {ex.Message}", ex);
        }
    }
}
