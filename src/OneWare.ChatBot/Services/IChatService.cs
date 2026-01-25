using System;
using System.Threading.Tasks;

namespace OneWare.ChatBot.Services;

public interface IChatService : IAsyncDisposable
{
    event EventHandler<ChatServiceMessageEvent>? MessageReceived;
    event EventHandler<ChatServiceStatusEvent>? StatusChanged;

    Task InitializeAsync(string model);
    Task SendAsync(string prompt);
    Task AbortAsync();
}
