using System;
using System.Threading.Tasks;
using OneWare.ChatBot.Models;

namespace OneWare.ChatBot.Services;

public interface IChatService : IAsyncDisposable
{
    public string Name { get; }
    
    event EventHandler<ChatServiceMessageEvent>? MessageReceived;
    event EventHandler<ChatServiceStatusEvent>? StatusChanged;
    
    Task<ModelModel[]> InitializeAsync();
    Task SendAsync(string modelId, string prompt);
    Task AbortAsync();
}
