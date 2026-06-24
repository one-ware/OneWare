namespace OneWare.Essentials.Enums;

/// <summary>
/// Determines how a chat message is delivered relative to an in-progress turn.
/// </summary>
public enum ChatSendMode
{
    /// <summary>
    /// Normal delivery. Starts a new turn when the agent is idle.
    /// </summary>
    Send,

    /// <summary>
    /// Steering: inject the message into the agent's current turn (immediate).
    /// </summary>
    Steer,

    /// <summary>
    /// Queueing: buffer the message and process it after the current turn finishes (enqueue).
    /// </summary>
    Queue
}
