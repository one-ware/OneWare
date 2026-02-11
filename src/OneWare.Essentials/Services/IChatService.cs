using System.ComponentModel;
using Avalonia.Controls;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IChatService : INotifyPropertyChanged, IAsyncDisposable
{
    /// <summary>
    /// Display name of the chat service.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Optional UI extension displayed under the chat input area.
    /// </summary>
    public Control? BottomUiExtension { get; }

    /// <summary>
    /// Fired when the chat session is reset.
    /// </summary>
    event EventHandler SessionReset;
    /// <summary>
    /// Fired when a chat event is received.
    /// </summary>
    event EventHandler<ChatEvent>? EventReceived;
    /// <summary>
    /// Fired when service status changes.
    /// </summary>
    event EventHandler<StatusEvent>? StatusChanged;
    
    /// <summary>
    /// Initializes the chat service (auth, clients, state).
    /// </summary>
    Task<bool> InitializeAsync();
    /// <summary>
    /// Sends a prompt to the chat service.
    /// </summary>
    Task SendAsync(string prompt);
    /// <summary>
    /// Aborts the current request.
    /// </summary>
    Task AbortAsync();
    /// <summary>
    /// Starts a new chat session.
    /// </summary>
    Task NewChatAsync();
}
