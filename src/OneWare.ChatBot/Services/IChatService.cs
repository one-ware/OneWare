using System;
using System.Threading.Tasks;

namespace OneWare.ChatBot.Services;

public interface IChatService : IAsyncDisposable
{
    event EventHandler<ChatServiceMessageEvent>? MessageReceived;
    event EventHandler<ChatServiceStatusEvent>? StatusChanged;
    
    Task<string[]> InitializeAsync();
    Task SendAsync(string model, string prompt);
    Task AbortAsync();
}
