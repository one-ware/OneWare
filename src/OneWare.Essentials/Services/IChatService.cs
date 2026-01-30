using System.ComponentModel;
using Avalonia.Controls;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IChatService : INotifyPropertyChanged, IAsyncDisposable
{
    public string Name { get; }
    public Control? BottomUiExtension { get; }
    
    event EventHandler<ChatServiceMessageEvent>? MessageReceived;
    event EventHandler<ChatServiceStatusEvent>? StatusChanged;
    
    Task<bool> AuthenticateAsync();
    Task<ChatInitializationStatus> InitializeAsync();
    Task SendAsync(string prompt);
    Task AbortAsync();
    Task NewChatAsync();
}
