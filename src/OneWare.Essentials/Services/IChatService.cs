using Avalonia.Controls;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IChatService : IAsyncDisposable
{
    public string Name { get; }
    
    public Control? UiExtension { get; }
    
    event EventHandler<ChatServiceMessageEvent>? MessageReceived;
    event EventHandler<ChatServiceStatusEvent>? StatusChanged;
    
    Task InitializeAsync();
    Task SendAsync(string prompt);
    Task AbortAsync();
    Task NewChatAsync();
}
