namespace OneWare.Essentials.Services;

/// <summary>
/// Optional extension for chat services that can run several chat sessions at the same time, e.g. in separate chat
/// tabs. The registered service is the first session; <see cref="CreateSession" /> creates more.
/// </summary>
public interface IChatServiceWithParallelSessions : IChatServiceWithSessions
{
    /// <summary>
    /// Creates an independent session of this service: it has its own conversation, model selection, UI extensions
    /// and events, but shares the backend connection (sign-in, model list) with the registered service. The caller
    /// initializes it with <see cref="IChatService.InitializeAsync" /> and disposes it when the session is closed;
    /// disposing it does not affect the registered service or other sessions.
    /// </summary>
    IChatService CreateSession();
}
