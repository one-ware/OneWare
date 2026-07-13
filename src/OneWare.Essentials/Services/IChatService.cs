using System.ComponentModel;
using Avalonia.Controls;
using OneWare.Essentials.Enums;
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
    /// Optional UI extension displayed above the chat input area (e.g. attachments).
    /// </summary>
    public Control? TopUiExtension => null;

    /// <summary>
    /// Optional UI extension displayed in the footer row beneath the chat input area,
    /// next to the chat service selector (e.g. token usage, approval mode).
    /// </summary>
    public Control? FooterUiExtension => null;

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
    /// Sends a prompt to the chat service using the given delivery mode.
    /// </summary>
    /// <param name="prompt">The message text.</param>
    /// <param name="mode">
    /// How the message is delivered relative to an in-progress turn. <see cref="ChatSendMode.Steer"/>
    /// injects into the current turn, <see cref="ChatSendMode.Queue"/> runs it after the current turn.
    /// </param>
    /// <remarks>
    /// Default implementation ignores <paramref name="mode"/> and forwards to <see cref="SendAsync(string)"/>,
    /// so existing implementers stay binary-compatible. Implementers that support steering/queueing
    /// should override this.
    /// </remarks>
    Task SendAsync(string prompt, ChatSendMode mode) => SendAsync(prompt);
    /// <summary>
    /// Aborts the current request.
    /// </summary>
    Task AbortAsync();
    /// <summary>
    /// Starts a new chat session.
    /// </summary>
    Task NewChatAsync();
}
