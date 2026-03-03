namespace OneWare.Essentials.Services;

/// <summary>
/// Optional extension for chat services that support loading/resuming chats by session id.
/// </summary>
public interface IChatServiceWithSessions : IChatService
{
    /// <summary>
    /// Current active provider session id.
    /// </summary>
    string? CurrentSessionId { get; }

    /// <summary>
    /// Loads/resumes an existing chat session by id.
    /// </summary>
    Task<bool> LoadSessionAsync(string sessionId);
}
