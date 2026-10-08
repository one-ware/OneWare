namespace OneWare.Essentials.Services;

/// <summary>
/// Optional extension for chat services whose stored chat history can become invalid, e.g. because a
/// different account logged in.
/// </summary>
public interface IChatServiceWithHistoryReset : IChatService
{
    /// <summary>
    /// Raised when all stored chats of this service must be discarded.
    /// </summary>
    event EventHandler HistoryCleared;
}
